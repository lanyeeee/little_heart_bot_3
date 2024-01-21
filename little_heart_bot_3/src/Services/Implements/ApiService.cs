using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using little_heart_bot_3.Crypto;
using little_heart_bot_3.Data.Models;
using little_heart_bot_3.Others;

namespace little_heart_bot_3.Services.Implements;

public abstract class ApiService : IApiService
{
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _options;
    private readonly HttpClient _httpClient;

    protected ApiService(
        ILogger logger,
        JsonSerializerOptions options,
        IHttpClientFactory httpclientFactory)
    {
        _options = options;
        _logger = logger;
        _httpClient = httpclientFactory.CreateClient("global");
    }

    public async Task<JsonNode> VerifyCookiesAsync(UserModel user,
        CancellationToken cancellationToken = default)
    {
        var requestMessage = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri("https://api.bilibili.com/x/web-interface/nav"),
            Headers = { { "Cookie", user.Cookie } }
        }.SetRetryCallback((outcome, retryDelay, retryCount) =>
        {
            _logger.LogDebug(outcome.Exception,
                "uid {Uid} 验证cookie时遇到异常，准备在 {RetryDelay} 秒后进行第 {RetryCount} 次重试",
                user.Uid,
                retryDelay.TotalSeconds,
                retryCount);
        });

        HttpResponseMessage responseMessage = await _httpClient.SendAsync(requestMessage, cancellationToken);
        return (await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, cancellationToken))!;
    }

    public async Task<JsonNode> GetPrivateMessagesAsync(BotModel bot,
        UserModel user,
        CancellationToken cancellationToken = default)
    {
        var uri = new Uri(
            $"https://api.vc.bilibili.com/svr_sync/v1/svr_sync/fetch_session_msgs?talker_id={user.Uid}&session_type=1");

        HttpRequestMessage requestMessage = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = uri,
            Headers = { { "Cookie", bot.Cookie } },
        }.SetRetryCallback((outcome, retryDelay, retryCount) =>
        {
            _logger.LogDebug(outcome.Exception,
                "获取与uid {Uid} 的聊天记录时遇到异常，准备在 {RetryDelay} 秒后进行第 {RetryCount} 次重试",
                user.Uid,
                retryDelay.TotalSeconds,
                retryCount);
        });
        HttpResponseMessage responseMessage = await _httpClient.SendAsync(requestMessage, cancellationToken);
        return (await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, cancellationToken))!;
    }

    public async Task<JsonNode> UpdateSignAsync(BotModel bot, string sign,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, string?>
        {
            { "user_sign", sign },
            { "jsonp", "jsonp" },
            { "csrf", bot.Csrf }
        };

        HttpRequestMessage requestMessage = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri("https://api.bilibili.com/x/member/web/sign/update"),
            Headers = { { "Cookie", bot.Cookie } },
            Content = new FormUrlEncodedContent(payload)
        }.SetRetryCallback((outcome, retryDelay, retryCount) =>
        {
            _logger.LogDebug(outcome.Exception,
                "更新签名时遇到异常，准备在 {RetryDelay} 秒后进行第 {RetryCount} 次重试",
                retryDelay.TotalSeconds,
                retryCount);
        });
        HttpResponseMessage responseMessage = await _httpClient.SendAsync(requestMessage, cancellationToken);
        return (await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, cancellationToken))!;
    }

    public async Task<JsonNode> SendPrivateMessageAsync(BotModel bot,
        UserModel user,
        string content,
        CancellationToken cancellationToken = default)
    {
        string queryString = await Wbi.GetWbiQueryStringAsync(_httpClient, cancellationToken: cancellationToken);

        var payload = new Dictionary<string, string?>
        {
            ["msg[sender_uid]"] = bot.Uid.ToString(),
            ["msg[receiver_id]"] = user.Uid.ToString(),
            ["msg[receiver_type]"] = "1",
            ["msg[msg_type]"] = "1",
            ["msg[dev_id]"] = bot.DevId,
            ["msg[timestamp]"] = DateTimeOffset.Now.ToUnixTimeSeconds().ToString(),
            ["msg[content]"] = new JsonObject { ["content"] = content }.ToJsonString(_options),
            ["csrf"] = bot.Csrf
        };

        HttpRequestMessage requestMessage = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri($"https://api.vc.bilibili.com/web_im/v1/web_im/send_msg?{queryString}"),
            Headers = { { "Cookie", bot.Cookie } },
            Content = new FormUrlEncodedContent(payload)
        }.SetRetryCallback((outcome, retryDelay, retryCount) =>
        {
            _logger.LogDebug(outcome.Exception,
                "给发送 {Uid} 私信时遇到异常，准备在 {RetryDelay} 秒后进行第 {RetryCount} 次重试",
                user.Uid,
                retryDelay.TotalSeconds,
                retryCount);
        });

        HttpResponseMessage responseMessage = await _httpClient.SendAsync(requestMessage, cancellationToken);

        return (await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, cancellationToken))!;
    }

    public async Task<JsonNode> GetNormalSessionListAsync(BotModel bot,
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
            _logger.LogDebug(outcome.Exception,
                "获取normal_session_list时遇到异常，准备在 {RetryDelay} 秒后进行第 {RetryCount} 次重试",
                retryDelay.TotalSeconds,
                retryCount);
        });

        HttpResponseMessage responseMessage = await _httpClient.SendAsync(requestMessage, cancellationToken);
        return (await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, cancellationToken))!;
    }

    public async Task<JsonNode> GetBlockedSessionListAsync(BotModel bot,
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
            _logger.LogDebug(outcome.Exception,
                "获取blocked_session_list时遇到异常，准备在 {RetryDelay} 秒后进行第 {RetryCount} 次重试",
                retryDelay.TotalSeconds,
                retryCount);
        });

        HttpResponseMessage responseMessage = await _httpClient.SendAsync(requestMessage, cancellationToken);
        return (await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, cancellationToken))!;
    }

    public async Task<JsonNode> ThumbsUpAsync(MessageModel message, CancellationToken cancellationToken = default)
    {
        UserModel user = message.UserModel;
        var payload = new Dictionary<string, string?>
        {
            { "roomid", message.RoomId.ToString() },
            { "csrf", user.Csrf },
            { "csrf_token", user.Csrf }
        };

        HttpRequestMessage requestMessage = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri("https://api.live.bilibili.com/xlive/web-ucenter/v1/interact/likeInteract"),
            Headers = { { "Cookie", user.Cookie } },
            Content = new FormUrlEncodedContent(payload)
        }.SetRetryCallback((outcome, retryDelay, retryCount) =>
        {
            _logger.LogDebug(outcome.Exception,
                "uid {Uid} 给 {TargetUid}({TargetName}) 点赞时遇到异常，准备在 {RetryDelay} 秒后进行第 {RetryCount} 次重试",
                message.Uid,
                message.TargetUid,
                message.TargetName,
                retryDelay.TotalSeconds,
                retryCount);
        });

        HttpResponseMessage responseMessage = await _httpClient.SendAsync(requestMessage, cancellationToken);
        return (await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, cancellationToken))!;
    }

    public async Task<JsonNode> PostMessageAsync(MessageModel message, CancellationToken cancellationToken = default)
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
        var requestMessage = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri("https://api.live.bilibili.com/msg/send"),
            Headers = { { "Cookie", user.Cookie } },
            Content = new FormUrlEncodedContent(payload)
        }.SetRetryCallback((outcome, retryDelay, retryCount) =>
        {
            _logger.LogDebug(outcome.Exception,
                "uid {Uid} 给 {TargetUid}({TargetName}) 发送弹幕时遇到异常，准备在 {RetryDelay} 秒后进行第 {RetryCount} 次重试",
                message.Uid,
                message.TargetUid,
                message.TargetName,
                retryDelay.TotalSeconds,
                retryCount);
        });

        HttpResponseMessage responseMessage = await _httpClient.SendAsync(requestMessage, cancellationToken);

        return (await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, cancellationToken))!;
    }

    public async Task<JsonNode> GetExpAsync(TargetModel target, CancellationToken cancellationToken = default)
    {
        var uri = new Uri(
            $"https://api.live.bilibili.com/xlive/app-ucenter/v1/fansMedal/fans_medal_info?target_id={target.TargetUid}");

        var requestMessage = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = uri,
            Headers = { { "cookie", target.UserModel.Cookie } }
        }.SetRetryCallback((outcome, retryDelay, retryCount) =>
        {
            _logger.LogDebug(outcome.Exception,
                "uid {Uid} 获取 {TargetUid}({TargetName}) 粉丝牌经验时遇到异常，准备在 {RetryDelay} 秒后进行第 {RetryCount} 次重试",
                target.Uid,
                target.TargetUid,
                target.TargetName,
                retryDelay.TotalSeconds,
                retryCount);
        });

        HttpResponseMessage responseMessage = await _httpClient.SendAsync(requestMessage, cancellationToken);
        return (await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, cancellationToken))!;
    }

    public async Task<JsonNode> GetEPayloadAsync(TargetModel target,
        CancellationToken cancellationToken = default)
    {
        var uri = new Uri(
            $"https://api.live.bilibili.com/xlive/web-room/v1/index/getInfoByRoom?&room_id={target.RoomId}");
        var requestMessage = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = uri
        }.SetRetryCallback((outcome, retryDelay, retryCount) =>
        {
            _logger.LogDebug(outcome.Exception,
                "获取E心跳包的payload时遇到异常，准备在 {RetryDelay} 秒后进行第 {RetryCount} 次重试",
                retryDelay.TotalSeconds,
                retryCount);
        });

        HttpResponseMessage responseMessage = await _httpClient.SendAsync(requestMessage, cancellationToken);
        return (await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, cancellationToken))!;
    }

    public async Task<JsonNode> PostEAsync(TargetModel target,
        Dictionary<string, string> ePayload,
        CancellationToken cancellationToken = default)
    {
        HttpRequestMessage requestMessage = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri("https://live-trace.bilibili.com/xlive/data-interface/v1/x25Kn/E"),
            Headers = { { "Cookie", target.UserModel.Cookie } },
            Content = new FormUrlEncodedContent(ePayload)
        }.SetRetryCallback((outcome, retryDelay, retryCount) =>
        {
            _logger.LogDebug(outcome.Exception,
                "uid {Uid} 发送E心跳包时遇到异常，准备在 {RetryDelay} 秒后进行第 {RetryCount} 次重试",
                target.Uid,
                retryDelay.TotalSeconds,
                retryCount);
        });

        HttpResponseMessage responseMessage = await _httpClient.SendAsync(requestMessage, cancellationToken);
        return (await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, cancellationToken))!;
    }

    public async Task<JsonNode> PostXAsync(TargetModel target,
        Dictionary<string, string> payload,
        CancellationToken cancellationToken = default)
    {
        HttpRequestMessage requestMessage = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri("https://live-trace.bilibili.com/xlive/data-interface/v1/x25Kn/X"),
            Headers = { { "Cookie", target.UserModel.Cookie } },
            Content = new FormUrlEncodedContent(payload)
        }.SetRetryCallback((outcome, retryDelay, retryCount) =>
        {
            _logger.LogDebug(outcome.Exception,
                "uid {Uid} 发送X心跳包时遇到异常，准备在 {RetryDelay} 秒后进行第 {RetryCount} 次重试",
                target.Uid,
                retryDelay.TotalSeconds,
                retryCount);
        });

        HttpResponseMessage responseMessage = await _httpClient.SendAsync(requestMessage, cancellationToken);
        return (await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, cancellationToken))!;
    }


    public async Task<JsonNode> GetOtherUserInfoAsync(UserModel user,
        long uid,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string> { { "mid", uid.ToString() } };
        string queryString = await Wbi.GetWbiQueryStringAsync(_httpClient, parameters, cancellationToken);

        var requestMessage = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri($"https://api.bilibili.com/x/space/wbi/acc/info?{queryString}"),
            Headers =
            {
                {
                    "user-agent",
                    "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/102.0.0.0 Safari/537.36"
                },
                { "cookie", user.Cookie }
            },
        }.SetRetryCallback((outcome, retryDelay, retryCount) =>
        {
            _logger.LogDebug(outcome.Exception,
                "uid {Uid} 获取 {TargetUid} 的用户数据遇到异常，准备在 {RetryDelay} 秒后进行第 {RetryCount} 次重试",
                user.Uid,
                uid,
                retryDelay.TotalSeconds,
                retryCount);
        });

        HttpResponseMessage responseMessage = await _httpClient.SendAsync(requestMessage, cancellationToken);
        await Task.Delay(1000, cancellationToken);
        return (await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, cancellationToken))!;
    }
}