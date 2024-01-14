using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using little_heart_bot_3.Crypto;
using little_heart_bot_3.Data.Models;
using little_heart_bot_3.Others;

namespace little_heart_bot_3.Services.Implements;

public class BotService : IBotService
{
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _options;
    private readonly HttpClient _httpClient;
    private readonly IUserService _userService;

    public BotService(
        ILogger<BotHostedService> logger,
        JsonSerializerOptions options,
        IHttpClientFactory httpClientFactory,
        [FromKeyedServices("bot:UserService")] IUserService userService)
    {
        _logger = logger;
        _options = options;
        _userService = userService;
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
                _logger.LogDebug(outcome.Exception,
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
                _logger.LogDebug(outcome.Exception,
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
                    () => _logger.LogCritical("小心心bot的Cookie已过期"),
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

    public async Task<bool> SendPrivateMessageAsync(BotModel bot,
        UserModel user,
        string content,
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
                _logger.LogDebug(outcome.Exception,
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

            user.ConfigTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            user.ConfigNum++;
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

    public async Task HandleCommandAsync(
        BotModel bot,
        UserModel user,
        string command,
        string? parameter,
        CancellationToken cancellationToken = default)
    {
        switch (command)
        {
            case "/target_set" when parameter is not null:
                await HandleTargetSetCommandAsync(user, parameter, cancellationToken);
                break;
            case "/target_delete" when parameter is not null:
                HandleTargetDeleteCommand(user, parameter);
                break;
            case "/message_set" when parameter is not null:
                await HandleMessageSetCommandAsync(user, parameter, cancellationToken);
                break;
            case "/message_delete" when parameter is not null:
                HandleMessageDeleteCommand(user, parameter);
                break;
            case "/cookie_commit" when parameter is not null:
                HandleCookieCommitCommand(user, parameter);
                break;
            case "/config_all":
                await HandleConfigAllCommandAsync(bot, user, cancellationToken);
                break;
            case "/message_config":
                await HandleMessageConfigCommandAsync(bot, user, parameter, cancellationToken);
                break;
            case "/target_config":
                await HandleTargetConfigCommandAsync(bot, user, parameter, cancellationToken);
                break;
            case "/delete":
                HandleDeleteCommand(user);
                break;
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

    private async Task<JsonArray?> GetNormalSessionListAsync(
        BotModel bot,
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

    private async Task<JsonArray?> GetBlockedSessionListAsync(
        BotModel bot,
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

    #region CommandHandler

    private async Task HandleTargetSetCommandAsync(
        UserModel user,
        string parameter,
        CancellationToken cancellationToken = default)
    {
        bool parameterIsUid = long.TryParse(parameter, out var targetUid);
        if (!parameterIsUid || user.Targets.Count > 50)
        {
            return;
        }

        JsonNode? data = await _userService.GetOtherUserInfoAsync(user, targetUid, cancellationToken);
        if (data is null)
        {
            return;
        }

        string targetName = (string)data["name"]!;
        long roomId = (long)data["live_room"]!["roomid"]!;
        TargetModel? target = user.Targets.Find(t => t.TargetUid == targetUid);

        if (target is not null)
        {
            target.TargetName = targetName;
            target.RoomId = roomId;
            target.Completed = false;
        }
        else
        {
            target = new TargetModel()
            {
                Uid = user.Uid,
                Completed = false,
                RoomId = roomId,
                TargetName = targetName,
                TargetUid = targetUid
            };
            user.Targets.Add(target);

            var message = user.Messages.Find(m => m.TargetUid == targetUid);

            if (message is not null)
            {
                message.Completed = false;
            }
            else
            {
                message = new MessageModel()
                {
                    Uid = user.Uid,
                    TargetName = targetName,
                    TargetUid = targetUid,
                    RoomId = roomId,
                    Code = 0,
                    Content = Globals.DefaultMessageContent,
                    Completed = false
                };
                user.Messages.Add(message);
            }
        }

        user.Completed = false;
    }

    private void HandleTargetDeleteCommand(UserModel user, string parameter)
    {
        if (parameter == "all")
        {
            user.Targets.Clear();
            return;
        }

        if (!long.TryParse(parameter, out var targetUid))
        {
            return;
        }

        TargetModel? target = user.Targets.Find(t => t.TargetUid == targetUid);
        if (target is not null)
        {
            user.Targets.Remove(target);
        }
    }

    private async Task HandleMessageSetCommandAsync(
        UserModel user,
        string parameter,
        CancellationToken cancellationToken = default)
    {
        string[] pair = parameter.Split(" ", 2);
        if (pair.Length != 2)
        {
            return;
        }

        string targetUidString = pair[0].Trim();
        bool targetUidStringIsUid = long.TryParse(targetUidString, out var targetUid);
        string content = pair[1].Trim();
        int messageCount = user.Messages.Count;
        if (!targetUidStringIsUid || content.Length > 20 || messageCount > 50)
        {
            return;
        }

        MessageModel? message = user.Messages.Find(m => m.TargetUid == targetUid);
        if (message is not null)
        {
            message.Content = content;
            message.Completed = false;
            message.Code = 0;
            message.Response = null;
        }
        else
        {
            JsonNode? data = await _userService.GetOtherUserInfoAsync(user, targetUid, cancellationToken);
            if (data is null)
            {
                return;
            }

            string targetName = (string)data["name"]!;
            long roomId = (long)data["live_room"]!["roomid"]!;

            message = new MessageModel
            {
                Uid = user.Uid,
                TargetUid = targetUid,
                TargetName = targetName,
                RoomId = roomId,
                Content = content,
                Code = 0
            };
            user.Messages.Add(message);
        }
    }

    private void HandleMessageDeleteCommand(UserModel user, string parameter)
    {
        if (parameter == "all")
        {
            //删掉所有没有对应target的message
            user.Messages.RemoveAll(m => !user.Targets.Exists(t => t.TargetUid == m.TargetUid));

            //剩下的message只需要把内容重置就行了
            foreach (var msg in user.Messages)
            {
                msg.Content = Globals.DefaultMessageContent;
                msg.Code = 0;
                msg.Response = null;
            }

            return;
        }

        if (!long.TryParse(parameter, out var targetUid))
        {
            return;
        }

        MessageModel? message = user.Messages.Find(m => m.TargetUid == targetUid);
        if (message is null)
        {
            return;
        }

        TargetModel? target = user.Targets.Find(t => t.TargetUid == targetUid);
        if (target is not null)
        {
            message.Content = Globals.DefaultMessageContent;
            message.Code = 0;
            message.Response = null;
        }
        else
        {
            user.Messages.Remove(message);
        }
    }

    private void HandleCookieCommitCommand(UserModel user, string parameter)
    {
        try
        {
            user.Cookie = parameter.Replace("\n", "");
            user.Csrf = Globals.GetCsrf(user.Cookie);
            user.CookieStatus = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "uid {uid} 提交的cookie有误", user.Uid);
        }
    }

    private async Task HandleConfigAllCommandAsync(
        BotModel bot,
        UserModel user,
        CancellationToken cancellationToken = default)
    {
        string? content = _userService.GetConfigAllString(user);
        if (content is null)
        {
            return;
        }

        await SendPrivateMessageAsync(bot, user, content, cancellationToken);
    }

    private async Task HandleMessageConfigCommandAsync(
        BotModel bot,
        UserModel user,
        string? parameter,
        CancellationToken cancellationToken = default)
    {
        if (parameter == "all")
        {
            List<string>? contents = _userService.GetAllMessageConfigStringSplit(user);
            if (contents is null)
            {
                return;
            }

            foreach (string message in contents)
            {
                string content = message + $"\n已用查询次数({user.ConfigNum + 1}/10)\n";
                await SendPrivateMessageAsync(bot, user, content, cancellationToken);
                await Task.Delay(1000, cancellationToken);
            }

            return;
        }

        if (long.TryParse(parameter, out var targetUid))
        {
            MessageModel? message = user.Messages.Find(m => m.TargetUid == targetUid);
            if (message is null)
            {
                return;
            }

            string? content = _userService.GetSpecifyMessageConfigString(message);
            if (content is null)
            {
                return;
            }

            content += $"\n已用查询次数({user.ConfigNum + 1}/10)\n";
            await SendPrivateMessageAsync(bot, user, content, cancellationToken);
        }
        else
        {
            string? content = _userService.GetAllMessageConfigString(user);
            if (content is null)
            {
                return;
            }

            if (content.Length > 450)
            {
                content =
                    "设置的弹幕过多，配置信息长度大于500，超过了私信长度的上限，无法发送\n\n请尝试使用\n/message_config 目标uid\n进行单个查询\n\n或者使用/message_config all\n获取分段的完整配置信息(每段消耗一次查询次数)";
            }

            content += $"\n已用查询次数({user.ConfigNum + 1}/10)\n";
            await SendPrivateMessageAsync(bot, user, content, cancellationToken);
        }
    }

    private async Task HandleTargetConfigCommandAsync(
        BotModel bot,
        UserModel user,
        string? parameter,
        CancellationToken cancellationToken = default)
    {
        if (parameter == "all")
        {
            List<string>? contents = _userService.GetAllTargetConfigStringSplit(user);
            if (contents is null)
            {
                return;
            }

            foreach (string message in contents)
            {
                string content = message + $"\n已用查询次数({user.ConfigNum + 1}/10)\n";
                await SendPrivateMessageAsync(bot, user, content, cancellationToken);
                await Task.Delay(1000, cancellationToken);
            }

            return;
        }

        if (long.TryParse(parameter, out var targetUid))
        {
            TargetModel? target = user.Targets.Find(t => t.TargetUid == targetUid);
            if (target is null)
            {
                return;
            }

            string? content = _userService.GetSpecifyTargetConfigString(target);
            if (content is null)
            {
                return;
            }

            content += $"\n已用查询次数({user.ConfigNum + 1}/10)\n";
            await SendPrivateMessageAsync(bot, user, content, cancellationToken);
        }
        else
        {
            string? content = _userService.GetAllTargetConfigString(user);
            if (content is null)
            {
                return;
            }

            if (content.Length > 450)
            {
                content =
                    "设置的目标过多，配置信息长度大于500，超过了私信长度的上限，无法发送\n\n请尝试使用\n/target_config 目标uid\n进行单个查询\n\n或者使用\n/target_config all\n获取分段的完整配置信息(每段消耗一次查询次数)\n";
            }

            content += $"\n已用查询次数({user.ConfigNum + 1}/10)\n";
            await SendPrivateMessageAsync(bot, user, content, cancellationToken);
        }
    }

    private void HandleDeleteCommand(UserModel user)
    {
        user.Cookie = "";
        user.Csrf = "";
        user.Completed = false;
        user.CookieStatus = CookieStatus.Error;
        user.Targets.Clear();
        user.Messages.Clear();
    }

    #endregion
}