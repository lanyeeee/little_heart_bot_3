﻿using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using little_heart_bot_3.Data;
using little_heart_bot_3.Data.Models;
using little_heart_bot_3.Others;
using Polly;
using Polly.Retry;

namespace little_heart_bot_3.Services.Implements;

public class MessageService : IMessageService
{
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _options;
    private readonly HttpClient _httpClient;
    private readonly IServiceProvider _provider;

    private readonly ResiliencePipeline _thumbsUpPipeline;
    private readonly ResiliencePipeline _sendPipeline;

    public MessageService(
        ILogger logger,
        JsonSerializerOptions options,
        HttpClient httpClient,
        IServiceProvider provider)
    {
        _logger = logger;
        _options = options;
        _httpClient = httpClient;
        _provider = provider;

        _thumbsUpPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<LittleHeartException>(ex => ex.Reason == Reason.NullResponse)
                    .Handle<HttpRequestException>(),
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                MaxRetryAttempts = 5,
                OnRetry = args =>
                {
                    var message = args.Context.Properties.GetValue(LittleHeartResilienceKeys.Message, null)!;
                    _logger.LogWarning(args.Outcome.Exception,
                        "uid {Uid} 给 {TargetName} 点赞时遇到异常，准备在 {RetryDelay} 秒后进行第 {AttemptNumber} 次重试",
                        message.Uid,
                        message.TargetName,
                        args.RetryDelay.TotalSeconds,
                        args.AttemptNumber);
                    return default;
                }
            })
            .Build();

        _sendPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<LittleHeartException>(ex => ex.Reason == Reason.NullResponse)
                    .Handle<HttpRequestException>(),
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                MaxRetryAttempts = 5,
                OnRetry = args =>
                {
                    var message = args.Context.Properties.GetValue(LittleHeartResilienceKeys.Message, null)!;
                    _logger.LogWarning(args.Outcome.Exception,
                        "uid {Uid} 给 {TargetName} 发送弹幕时遇到异常，准备在 {RetryDelay} 秒后进行第 {AttemptNumber} 次重试",
                        message.Uid,
                        message.TargetName,
                        args.RetryDelay.TotalSeconds,
                        args.AttemptNumber);
                    return default;
                }
            })
            .Build();
    }

    public async Task SendAsync(MessageModel message, CancellationToken cancellationToken = default)
    {
        if (message.Completed || message.Code != 0)
        {
            return;
        }

        var context = ResilienceContextPool.Shared.Get(cancellationToken);
        context.Properties.Set(LittleHeartResilienceKeys.Message, message);

        try
        {
            await _sendPipeline.ExecuteAsync(async ctx =>
            {
                JsonNode? response = await PostMessageAsync(message, ctx.CancellationToken);
                if (response is null)
                {
                    throw new LittleHeartException(Reason.NullResponse);
                }

                await HandleSendResponseAsync(message, response);

                await Task.Delay(3000, ctx.CancellationToken);
            }, context);
        }
        catch (LittleHeartException ex)
        {
            switch (ex.Reason)
            {
                case Reason.Ban:
                case Reason.CookieExpired:
                    throw;
                case Reason.NullResponse:
                    _logger.LogError("uid {Uid} 给 {TargetName} 发送弹幕时出现 NullResponse 异常，重试多次后依然失败",
                        message.Uid,
                        message.TargetName);
                    ex.Reason = Reason.Ban;
                    throw;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "uid {Uid} 给 {TargetName} 发送弹幕时出现 HttpRequestException 异常，重试多次后依然失败",
                message.Uid,
                message.TargetName);
            throw new LittleHeartException(Reason.Ban);
        }
        catch (TaskCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "uid {Uid} 给 {TargetName} 发送消息时出现预料之外的错误",
                message.Uid,
                message.TargetName);
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }


    public async Task ThumbsUpAsync(MessageModel message, CancellationToken cancellationToken = default)
    {
        UserModel user = message.UserModel;
        var payload = new Dictionary<string, string?>
        {
            { "roomid", message.RoomId.ToString() },
            { "csrf", user.Csrf },
            { "csrf_token", user.Csrf }
        };

        var context = ResilienceContextPool.Shared.Get(cancellationToken);
        context.Properties.Set(LittleHeartResilienceKeys.Message, message);

        try
        {
            await _thumbsUpPipeline.ExecuteAsync(async ctx =>
            {
                HttpResponseMessage responseMessage = await _httpClient.SendAsync(new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri("https://api.live.bilibili.com/xlive/web-ucenter/v1/interact/likeInteract"),
                    Headers = { { "Cookie", user.Cookie } },
                    Content = new FormUrlEncodedContent(payload)
                }, ctx.CancellationToken);
                JsonNode response =
                    await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, ctx.CancellationToken) ??
                    throw new LittleHeartException(Reason.NullResponse);

                int code = (int)response["code"]!;

                if (code == 0)
                {
#if DEBUG
                    Console.WriteLine($"uid {message.Uid} 给 {message.TargetName} 点赞成功");
#endif
                    _logger.LogInformation("uid {Uid} 给 {TargetName} 点赞成功",
                        message.Uid,
                        message.TargetName);
                }
                else if (code is -111 or -101)
                {
                    _logger.LogWithResponse(
                        () => _logger.LogWarning("uid {Uid} 给 {TargetName} 点赞失败，因为Cookie错误或已过期",
                            message.Uid,
                            message.TargetName),
                        response.ToJsonString(_options));

                    throw new LittleHeartException(Reason.CookieExpired);
                }
                else
                {
                    _logger.LogWithResponse(
                        () => _logger.LogError("uid {Uid} 给 {TargetName} 点赞失败，预料之外的错误",
                            message.Uid,
                            message.TargetName),
                        response.ToJsonString(_options));

                    throw new LittleHeartException(Reason.Ban);
                }
            }, context);
        }
        catch (LittleHeartException ex)
        {
            switch (ex.Reason)
            {
                case Reason.Ban:
                case Reason.CookieExpired:
                    throw;
                case Reason.NullResponse:
                    _logger.LogError("uid {Uid} 给 {TargetName} 点赞时出现 NullResponse 异常，重试多次后依然失败",
                        message.Uid,
                        message.TargetName);
                    ex.Reason = Reason.Ban;
                    throw;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "uid {Uid} 给 {TargetName} 点赞时出现 HttpRequestException 异常，重试多次后依然失败",
                message.Uid,
                message.TargetName);
            throw new LittleHeartException(Reason.Ban);
        }
        catch (TaskCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "uid {Uid} 给 {TargetName} 点赞时出现预料之外的错误",
                message.Uid,
                message.TargetName);
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }

    /// <exception cref="LittleHeartException">
    /// <br/>Reason.Ban
    /// <br/>Reason.CookieExpired
    /// </exception>
    private async Task HandleSendResponseAsync(MessageModel message, JsonNode response)
    {
        //不管结果，一条弹幕只发一次
        await using var db = _provider.GetRequiredService<LittleHeartDbContext>();
        db.Attach(message);
        message.Completed = true;
        message.Code = (int)response["code"]!;
        message.Response = response.ToString();
        await db.SaveChangesAsync(CancellationToken.None);

        if (message.Code == 0)
        {
#if DEBUG
            Console.WriteLine($"uid {message.Uid} 给 {message.TargetName} 发送弹幕成功");
#endif
            _logger.LogInformation("uid {Uid} 给 {TargetName} 发送弹幕成功",
                message.Uid,
                message.TargetName);
        }
        else if (message.Code == -412) //风控
        {
            _logger.LogWithResponse(
                () => _logger.LogWarning("uid {Uid} 给 {TargetName} 发送弹幕失败，因为风控",
                    message.Uid,
                    message.TargetName),
                response.ToJsonString(_options));
            throw new LittleHeartException(response.ToJsonString(_options), Reason.Ban);
        }
        else if (message.Code is -111 or -101) //Cookie过期
        {
            _logger.LogWithResponse(
                () => _logger.LogWarning("uid {Uid} 给 {TargetName} 发送弹幕失败，因为Cookie过期",
                    message.Uid,
                    message.TargetName),
                response.ToJsonString(_options));
            throw new LittleHeartException(response.ToJsonString(_options), Reason.CookieExpired);
        }
        else if (message.Code == -403) //可能是等级墙，也可能是全体禁言
        {
            _logger.LogWithResponse(
                () => _logger.LogWarning("uid {Uid} 给 {TargetName} 发送弹幕失败，因为主播开启了禁言",
                    message.Uid,
                    message.TargetName),
                response.ToJsonString(_options));
        }
        else if (message.Code == 11000) //似乎跟Up主的身份有关系
        {
            _logger.LogWithResponse(
                () => _logger.LogWarning("uid {Uid} 给 {TargetName} 发送弹幕失败，原因未知",
                    message.Uid,
                    message.TargetName),
                response.ToJsonString(_options));
        }
        else if (message.Code == 10030) //发弹幕的频率过高
        {
            _logger.LogWithResponse(
                () => _logger.LogWarning("uid {Uid} 给 {TargetName} 发送弹幕失败，因为发送弹幕的频率过高",
                    message.Uid,
                    message.TargetName),
                response.ToJsonString(_options));
        }
        else if (message.Code == 10023) //用户已将主播拉黑
        {
            _logger.LogWithResponse(
                () => _logger.LogWarning("uid {Uid} 给 {TargetName} 发送弹幕失败，因为用户已将主播拉黑",
                    message.Uid,
                    message.TargetName),
                response.ToJsonString(_options));
        }
        else if (message.Code == 1003) //用户已在本房间被禁言
        {
            _logger.LogWithResponse(
                () => _logger.LogWarning("uid {Uid} 给 {TargetName} 发送弹幕失败，因为用户已在本房间被禁言",
                    message.Uid,
                    message.TargetName), response.ToJsonString(_options));
        }
        else if (message.Code == 10024) //因主播隐私设置，暂无法发送弹幕
        {
            _logger.LogWithResponse(
                () => _logger.LogWarning("uid {Uid} 给 {TargetName} 发送弹幕失败，因为主播隐私设置，暂无法发送弹幕",
                    message.Uid,
                    message.TargetName),
                response.ToJsonString(_options));
        }
        else
        {
            _logger.LogWithResponse(
                () => _logger.LogError("uid {Uid} 给 {TargetName} 发送弹幕失败，预料之外的错误",
                    message.Uid,
                    message.TargetName),
                response.ToJsonString(_options));
            throw new LittleHeartException(response.ToJsonString(_options), Reason.Ban);
        }
    }

    private async Task<JsonNode?> PostMessageAsync(MessageModel message, CancellationToken cancellationToken = default)
    {
        UserModel user = message.UserModel;
        var payload = new Dictionary<string, string?>
        {
            { "bubble", "0" },
            { "msg", message.Content },
            { "color", "16777215" },
            { "mode", "1" },
            { "fontsize", "25" },
            { "rnd", DateTimeOffset.Now.ToUnixTimeSeconds().ToString() },
            { "roomid", message.RoomId.ToString() },
            { "csrf", user.Csrf },
            { "csrf_token", user.Csrf }
        };

        HttpResponseMessage responseMessage = await _httpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri("https://api.live.bilibili.com/msg/send"),
            Headers = { { "Cookie", user.Cookie } },
            Content = new FormUrlEncodedContent(payload)
        }, cancellationToken);

        return await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, cancellationToken);
    }
}