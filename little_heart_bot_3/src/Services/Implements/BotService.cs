using System.Text.Json;
using System.Text.Json.Nodes;
using little_heart_bot_3.Data.Models;
using little_heart_bot_3.Others;
using Polly;
using Polly.Retry;
using Serilog;

namespace little_heart_bot_3.Services.Implements;

public class BotService : IBotService
{
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _options;
    private readonly HttpClient _httpClient;

    private readonly ResiliencePipeline _getSessionListPipeline;
    private readonly ResiliencePipeline _updateSignPipeline;
    private readonly ResiliencePipeline _getMessagePipeline;
    private readonly ResiliencePipeline _sendMessagePipeline;


    #region Public

    public BotService(ILogger logger,
        JsonSerializerOptions options,
        HttpClient httpClient)
    {
        _logger = logger;

        _options = options;
        _httpClient = httpClient;

        _getSessionListPipeline = new ResiliencePipelineBuilder()
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
                    _logger.Warning(args.Outcome.Exception,
                        "获取session_list时遇到异常，准备在 {RetryDelay} 秒后进行第 {AttemptNumber} 次重试",
                        args.RetryDelay.TotalSeconds,
                        args.AttemptNumber);
                    return default;
                }
            })
            .Build();

        _updateSignPipeline = new ResiliencePipelineBuilder()
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
                    _logger.Warning(args.Outcome.Exception,
                        "更新签名时遇到异常，准备在 {RetryDelay} 秒后进行第 {AttemptNumber} 次重试",
                        args.RetryDelay.TotalSeconds,
                        args.AttemptNumber);
                    return default;
                }
            })
            .Build();

        _getMessagePipeline = new ResiliencePipelineBuilder()
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
                    var user = args.Context.Properties.GetValue(LittleHeartResilienceKeys.User, null)!;
                    _logger.Warning(args.Outcome.Exception,
                        "获取与uid {Uid} 的聊天记录时遇到异常，准备在 {RetryDelay} 秒后进行第 {AttemptNumber} 次重试",
                        user.Uid,
                        args.RetryDelay.TotalSeconds,
                        args.AttemptNumber);
                    return default;
                }
            })
            .Build();

        _sendMessagePipeline = new ResiliencePipelineBuilder()
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
                    _logger.Warning(args.Outcome.Exception,
                        "发送私信时遇到异常，准备在 {RetryDelay} 秒后进行第 {AttemptNumber} 次重试",
                        args.RetryDelay.TotalSeconds,
                        args.AttemptNumber);
                    return default;
                }
            })
            .Build();
    }

    public async Task<IEnumerable<JsonNode?>?> GetSessionListAsync(BotModel bot,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _getSessionListPipeline.ExecuteAsync(async token =>
            {
                var normalSessionList = await GetNormalSessionListAsync(bot, token);
                var blockedSessionList = await GetBlockedSessionListAsync(bot, token);

                if (normalSessionList == null)
                {
                    return blockedSessionList;
                }

                if (blockedSessionList == null)
                {
                    return normalSessionList;
                }

                //如果两个都不为空
                return normalSessionList.Union(blockedSessionList);
            }, cancellationToken);
        }
        catch (LittleHeartException ex)
        {
            switch (ex.Reason)
            {
                case Reason.Ban:
                case Reason.CookieExpired:
                    throw;
                case Reason.NullResponse:
                    _logger.Error("获取 session_list 时遇到 NullResponse 异常，重试多次后依然失败");
                    ex.Reason = Reason.Ban;
                    throw;
                default:
                    _logger.Fatal("如果出现这个错误，说明代码编写有问题");
                    return null;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.Error(ex,
                "获取 session_list 时遇到 HttpRequestException 异常，重试多次后依然失败");
            throw new LittleHeartException(Reason.Ban);
        }
        catch (TaskCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Fatal(ex,
                "获取 session_list 时出现预料之外的错误");
            return null;
        }
    }

    public async Task<IEnumerable<JsonNode?>?> GetMessagesAsync(BotModel bot, UserModel user,
        CancellationToken cancellationToken = default)
    {
        var uri = new Uri(
            $"https://api.vc.bilibili.com/svr_sync/v1/svr_sync/fetch_session_msgs?talker_id={user.Uid}&session_type=1");

        var context = ResilienceContextPool.Shared.Get(cancellationToken);
        context.Properties.Set(LittleHeartResilienceKeys.User, user);

        try
        {
            return await _getMessagePipeline.ExecuteAsync(async ctx =>
            {
                HttpResponseMessage responseMessage = await _httpClient.SendAsync(new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = uri,
                    Headers = { { "Cookie", bot.Cookie } },
                }, ctx.CancellationToken);
                await Task.Delay(1000, ctx.CancellationToken);
                JsonNode? response =
                    JsonNode.Parse(await responseMessage.Content.ReadAsStringAsync(ctx.CancellationToken));
                if (response == null)
                {
                    throw new LittleHeartException(Reason.NullResponse);
                }

                int? code = (int?)response["code"];

                if (code != 0)
                {
                    //TODO: 以后需要记录 风控 和 Cookie过期 的code，专门处理
                    _logger.ForContext("Response", response.ToJsonString(_options))
                        .Error("获取 {uid} 的聊天记录失败",
                            user.Uid);
                    throw new LittleHeartException(Reason.Ban);
                }

                return response["data"]!["messages"]?.AsArray().Reverse();
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
                    _logger.Error("获取uid {Uid} 的聊天记录时遇到 NullResponse 异常，重试多次后依然失败",
                        user.Uid);
                    ex.Reason = Reason.Ban;
                    throw;
                default:
                    _logger.Fatal("如果出现这个错误，说明代码编写有问题");
                    return null;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.Error(ex,
                "获取uid {Uid} 的聊天记录时遇到 HttpRequestException 异常，重试多次后依然失败",
                user.Uid);
            throw new LittleHeartException(Reason.Ban);
        }
        catch (TaskCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Fatal(ex,
                "获取uid {Uid} 的聊天记录时出现预料之外的错误",
                user.Uid);
            return null;
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }

    public async Task UpdateSignAsync(BotModel bot, CancellationToken cancellationToken = default)
    {
        if (!ShouldUpdateSign(bot))
        {
            return;
        }

        //需要更新
        string sign = MakeSign();

        var payload = new Dictionary<string, string?>
        {
            { "user_sign", sign },
            { "jsonp", "jsonp" },
            { "csrf", bot.Csrf }
        };
        try
        {
            await _updateSignPipeline.ExecuteAsync(async token =>
            {
                HttpResponseMessage responseMessage = await _httpClient.SendAsync(new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri("https://api.bilibili.com/x/member/web/sign/update"),
                    Headers = { { "Cookie", bot.Cookie } },
                    Content = new FormUrlEncodedContent(payload)
                }, token);
                JsonNode? response = JsonNode.Parse(await responseMessage.Content.ReadAsStringAsync(token));
                if (response == null)
                {
                    throw new LittleHeartException(Reason.NullResponse);
                }

                int? code = (int?)response["code"];
                if (code == 0)
                {
                    _logger.Information("签名改为：{sign}", sign);

                    bot.AppStatus = Globals.AppStatus;
                    bot.ReceiveStatus = Globals.ReceiveStatus;
                    bot.SendStatus = Globals.SendStatus;
                }
                else if (code == -111)
                {
                    _logger.ForContext("Response", response.ToJsonString(_options))
                        .Warning("小心心bot的Cookie已过期");
                    throw new LittleHeartException(Reason.CookieExpired);
                }
            }, cancellationToken);
        }
        catch (LittleHeartException ex)
        {
            switch (ex.Reason)
            {
                case Reason.Ban:
                case Reason.CookieExpired:
                    throw;
                case Reason.NullResponse:
                    _logger.Error("小心心bot更新签名时出现 NullResponse 异常，重试多次后依然失败");
                    ex.Reason = Reason.Ban;
                    throw;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.Error(ex,
                "小心心bot更新签名时出现 HttpRequestException 异常，重试多次后依然失败");
            throw new LittleHeartException(Reason.Ban);
        }
        catch (TaskCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Fatal(ex, "小心心bot更新签名时出现预料之外的错误");
        }
    }

    public async Task<bool> SendMessageAsync(BotModel bot, string content, UserModel user,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _sendMessagePipeline.ExecuteAsync(async _ =>
            {
                string queryString =
                    await Wbi.GetWbiQueryStringAsync(_httpClient, cancellationToken: cancellationToken);

                var payload = new Dictionary<string, string?>
                {
                    { "msg[sender_uid]", bot.Uid.ToString() },
                    { "msg[receiver_id]", user.Uid.ToString() },
                    { "msg[receiver_type]", "1" },
                    { "msg[msg_type]", "1" },
                    { "msg[dev_id]", bot.DevId },
                    { "msg[timestamp]", DateTimeOffset.Now.ToUnixTimeSeconds().ToString() },
                    { "msg[content]", new JsonObject { { "content", content } }.ToJsonString(_options) },
                    { "csrf", bot.Csrf }
                };

                HttpResponseMessage responseMessage = await _httpClient.SendAsync(new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"https://api.vc.bilibili.com/web_im/v1/web_im/send_msg?{queryString}"),
                    Headers = { { "Cookie", bot.Cookie } },
                    Content = new FormUrlEncodedContent(payload)
                }, cancellationToken);

                await Task.Delay(1000, cancellationToken);

                JsonNode? response = JsonNode.Parse(await responseMessage.Content.ReadAsStringAsync(cancellationToken));
                if (response == null)
                {
                    throw new LittleHeartException(Reason.NullResponse);
                }

                int? code = (int?)response["code"];
                //TODO: 后续还要记录 1.私信到达上限 2.cookie过期 3.风控 的code
                if (code != 0)
                {
                    _logger.ForContext("Response", response.ToJsonString(_options))
                        .Error("给uid {Uid} 发送私信失败",
                            user.Uid);

                    return false;
                }

                return true;
            }, cancellationToken);
        }
        catch (LittleHeartException ex)
        {
            switch (ex.Reason)
            {
                case Reason.Ban:
                case Reason.CookieExpired:
                    throw;
                case Reason.NullResponse:
                    _logger.Error("给uid {Uid} 发送私信时出现 NullResponse 异常，重试多次后依然失败，私信的内容为:{Content}",
                        user.Uid,
                        content);
                    ex.Reason = Reason.Ban;
                    throw;
                default:
                    _logger.Fatal("如果出现这个错误，说明代码编写有问题");
                    throw;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.Error(ex, "给uid {Uid} 发送私信时出现 HttpRequestException 异常，重试多次后依然失败，私信的内容为:{Content}",
                user.Uid,
                content);
            throw new LittleHeartException(Reason.Ban);
        }
        catch (TaskCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Fatal(ex,
                "给uid {Uid} 发送私信时出现预料之外的错误，今日停止发送私信，私信的内容为:{Content}",
                user.Uid,
                content);
            return false;
        }
    }

    #endregion

    #region Private

    private bool ShouldUpdateSign(BotModel bot)
    {
        //有一个不相等就需要更新
        return bot.AppStatus != Globals.AppStatus ||
               bot.ReceiveStatus != Globals.ReceiveStatus ||
               bot.SendStatus != Globals.SendStatus;
    }

    private string MakeSign()
    {
        string sign = "给你【";
        switch (Globals.AppStatus)
        {
            case AppStatus.Normal:
                sign += "弹幕、点赞、观看直播正常";
                break;
            case AppStatus.Cooling:
                sign += "弹幕、点赞、观看直播冷却中";
                break;
        }

        sign += "，";


        switch (Globals.ReceiveStatus)
        {
            case ReceiveStatus.Normal:
                sign += "接收私信正常";
                break;
            case ReceiveStatus.Cooling:
                sign += "接收私信冷却中";
                break;
        }

        sign += "，";

        switch (Globals.SendStatus)
        {
            case SendStatus.Normal:
                sign += "发送私信正常";
                break;
            case SendStatus.Cooling:
                sign += "发送私信冷却中";
                break;
            case SendStatus.Forbidden:
                sign += "发送私信已禁言";
                break;
        }

        sign += "】";
        return sign;
    }

    private async Task<JsonArray?> GetNormalSessionListAsync(BotModel bot,
        CancellationToken cancellationToken = default)
    {
        //普通的私信session
        HttpResponseMessage responseMessage = await _httpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri =
                new Uri("https://api.vc.bilibili.com/session_svr/v1/session_svr/get_sessions?session_type=1"),
            Headers = { { "Cookie", bot.Cookie } },
        }, cancellationToken);
        await Task.Delay(1000, cancellationToken);
        JsonNode? response = JsonNode.Parse(await responseMessage.Content.ReadAsStringAsync(cancellationToken));
        if (response == null)
        {
            throw new LittleHeartException(Reason.NullResponse);
        }

        int code = (int)response["code"]!;
        if (code != 0)
        {
            //TODO: 以后需要记录风控的code，专门处理
            _logger.ForContext("Response", response.ToJsonString(_options))
                .Error("获取普通的session_list失败");
            throw new LittleHeartException(Reason.Ban);
        }

        var sessionList = (JsonArray?)response["data"]!["session_list"];
        return sessionList;
    }

    private async Task<JsonArray?> GetBlockedSessionListAsync(BotModel bot,
        CancellationToken cancellationToken = default)
    {
        //被屏蔽的私信session
        HttpResponseMessage responseMessage = await _httpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri =
                new Uri("https://api.vc.bilibili.com/session_svr/v1/session_svr/get_sessions?session_type=5"),
            Headers = { { "Cookie", bot.Cookie } },
        }, cancellationToken);
        JsonNode? response = JsonNode.Parse(await responseMessage.Content.ReadAsStringAsync(cancellationToken));

        if (response == null)
        {
            throw new LittleHeartException(Reason.NullResponse);
        }

        int code = (int)response["code"]!;
        if (code != 0)
        {
            //TODO: 以后需要记录风控的code，专门处理
            _logger.ForContext("Response", response.ToJsonString(_options))
                .Error("获取被屏蔽的session_list失败");
            throw new LittleHeartException(Reason.Ban);
        }

        var blockedList = (JsonArray?)response["data"]!["session_list"];
        return blockedList;
    }

    #endregion
}