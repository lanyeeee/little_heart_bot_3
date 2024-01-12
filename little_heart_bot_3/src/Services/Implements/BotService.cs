using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using little_heart_bot_3.Data.Models;
using little_heart_bot_3.Others;

namespace little_heart_bot_3.Services.Implements;

public class BotService : IBotService
{
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _options;
    private readonly HttpClient _httpClient;

    public BotService([FromKeyedServices("bot:Logger")] ILogger logger,
        JsonSerializerOptions options,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _options = options;
        _httpClient = httpClientFactory.CreateClient("global");
    }

    public async Task<IEnumerable<JsonNode?>?> GetSessionListAsync(BotModel bot,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalSessionList = await GetNormalSessionListAsync(bot, cancellationToken);
            var blockedSessionList = await GetBlockedSessionListAsync(bot, cancellationToken);

            if (normalSessionList is null)
            {
                return blockedSessionList;
            }

            if (blockedSessionList is null)
            {
                return normalSessionList;
            }

            //如果两个都不为空
            return normalSessionList.Union(blockedSessionList);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "获取 session_list 时遇到 HttpRequestException 异常，重试多次后依然失败");
            throw new LittleHeartException(Reason.Ban);
        }
        catch (TaskCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex,
                "获取 session_list 时出现预料之外的错误");
            return null;
        }
    }

    public async Task<IEnumerable<JsonNode?>?> GetPrivateMessagesAsync(BotModel bot, UserModel user,
        CancellationToken cancellationToken = default)
    {
        var uri = new Uri(
            $"https://api.vc.bilibili.com/svr_sync/v1/svr_sync/fetch_session_msgs?talker_id={user.Uid}&session_type=1");

        try
        {
            HttpRequestMessage requestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = uri,
                Headers = { { "Cookie", bot.Cookie } },
            }.SetRetryCallback((outcome, retryDelay, retryCount) =>
            {
                _logger.LogWarning(outcome.Exception,
                    "获取与uid {Uid} 的聊天记录时遇到异常，准备在 {RetryDelay} 秒后进行第 {RetryCount} 次重试",
                    user.Uid,
                    retryDelay.TotalSeconds,
                    retryCount);
            });
            HttpResponseMessage responseMessage = await _httpClient.SendAsync(requestMessage, cancellationToken);
            await Task.Delay(1000, cancellationToken);
            var response = (await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, cancellationToken))!;

            int code = (int)response["code"]!;

            if (code != 0)
            {
                _logger.LogWithResponse(
                    () => _logger.LogError("获取 {uid} 的聊天记录失败", user.Uid),
                    response.ToJsonString(_options));
                throw new LittleHeartException(Reason.Ban);
            }

            return response["data"]!["messages"]?.AsArray().Reverse();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
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
            _logger.LogCritical(ex,
                "获取uid {Uid} 的聊天记录时出现预料之外的错误",
                user.Uid);
            return null;
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
            HttpRequestMessage requestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://api.bilibili.com/x/member/web/sign/update"),
                Headers = { { "Cookie", bot.Cookie } },
                Content = new FormUrlEncodedContent(payload)
            }.SetRetryCallback((outcome, retryDelay, retryCount) =>
            {
                _logger.LogWarning(outcome.Exception,
                    "更新签名时遇到异常，准备在 {RetryDelay} 秒后进行第 {RetryCount} 次重试",
                    retryDelay.TotalSeconds,
                    retryCount);
            });
            HttpResponseMessage responseMessage = await _httpClient.SendAsync(requestMessage, cancellationToken);
            var response = (await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, cancellationToken))!;

            int code = (int)response["code"]!;
            if (code == 0)
            {
                _logger.LogInformation("签名改为：{sign}", sign);

                bot.AppStatus = Globals.AppStatus;
                bot.ReceiveStatus = Globals.ReceiveStatus;
                bot.SendStatus = Globals.SendStatus;
            }
            else if (code == -111)
            {
                _logger.LogWithResponse(
                    () => _logger.LogWarning("小心心bot的Cookie已过期"),
                    response.ToJsonString(_options));

                throw new LittleHeartException(Reason.CookieExpired);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "小心心bot更新签名时出现 HttpRequestException 异常，重试多次后依然失败");
            throw new LittleHeartException(Reason.Ban);
        }
        catch (TaskCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "小心心bot更新签名时出现预料之外的错误");
        }
    }

    public async Task<bool> SendPrivateMessageAsync(BotModel bot, string content, UserModel user,
        CancellationToken cancellationToken = default)
    {
        try
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

            HttpRequestMessage requestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri($"https://api.vc.bilibili.com/web_im/v1/web_im/send_msg?{queryString}"),
                Headers = { { "Cookie", bot.Cookie } },
                Content = new FormUrlEncodedContent(payload)
            }.SetRetryCallback((outcome, retryDelay, retryCount) =>
            {
                _logger.LogWarning(outcome.Exception,
                    "给发送 {Uid} 私信时遇到异常，准备在 {RetryDelay} 秒后进行第 {RetryCount} 次重试",
                    user.Uid,
                    retryDelay.TotalSeconds,
                    retryCount);
            });

            HttpResponseMessage responseMessage = await _httpClient.SendAsync(requestMessage, cancellationToken);
            await Task.Delay(1000, cancellationToken);
            var response = (await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, cancellationToken))!;

            int code = (int)response["code"]!;

            if (code != 0)
            {
                _logger.LogWithResponse(
                    () => _logger.LogError("给uid {Uid} 发送私信失败", user.Uid),
                    response.ToJsonString(_options));

                return false;
            }

            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "给uid {Uid} 发送私信时出现 HttpRequestException 异常，重试多次后依然失败，私信的内容为:{Content}",
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
            _logger.LogCritical(ex,
                "给uid {Uid} 发送私信时出现预料之外的错误，今日停止发送私信，私信的内容为:{Content}",
                user.Uid,
                content);
            return false;
        }
    }


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
        HttpRequestMessage requestMessage = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri =
                new Uri("https://api.vc.bilibili.com/session_svr/v1/session_svr/get_sessions?session_type=1"),
            Headers = { { "Cookie", bot.Cookie } },
        }.SetRetryCallback((outcome, retryDelay, retryCount) =>
        {
            _logger.LogWarning(outcome.Exception,
                "获取normal_session_list时遇到异常，准备在 {RetryDelay} 秒后进行第 {RetryCount} 次重试",
                retryDelay.TotalSeconds,
                retryCount);
        });

        HttpResponseMessage responseMessage = await _httpClient.SendAsync(requestMessage, cancellationToken);
        await Task.Delay(1000, cancellationToken);
        JsonNode response = (await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, cancellationToken))!;

        int code = (int)response["code"]!;
        if (code != 0)
        {
            _logger.LogWithResponse(
                () => _logger.LogError("获取普通的session_list失败"),
                response.ToJsonString(_options));

            throw new LittleHeartException(Reason.Ban);
        }

        var sessionList = (JsonArray?)response["data"]!["session_list"];
        return sessionList;
    }

    private async Task<JsonArray?> GetBlockedSessionListAsync(BotModel bot,
        CancellationToken cancellationToken = default)
    {
        //被屏蔽的私信session
        HttpRequestMessage requestMessage = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri =
                new Uri("https://api.vc.bilibili.com/session_svr/v1/session_svr/get_sessions?session_type=5"),
            Headers = { { "Cookie", bot.Cookie } },
        }.SetRetryCallback((outcome, retryDelay, retryCount) =>
        {
            _logger.LogWarning(outcome.Exception,
                "获取blocked_session_list时遇到异常，准备在 {RetryDelay} 秒后进行第 {RetryCount} 次重试",
                retryDelay.TotalSeconds,
                retryCount);
        });

        HttpResponseMessage responseMessage = await _httpClient.SendAsync(requestMessage, cancellationToken);
        JsonNode response = (await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, cancellationToken))!;

        int code = (int)response["code"]!;
        if (code != 0)
        {
            _logger.LogWithResponse(
                () => _logger.LogError("获取被屏蔽的session_list失败"),
                response.ToJsonString(_options));

            throw new LittleHeartException(Reason.Ban);
        }

        var blockedList = (JsonArray?)response["data"]!["session_list"];
        return blockedList;
    }
}