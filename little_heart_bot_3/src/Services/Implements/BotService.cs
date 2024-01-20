using System.Text.Json;
using System.Text.Json.Nodes;
using little_heart_bot_3.Data;
using little_heart_bot_3.Data.Models;
using little_heart_bot_3.Others;
using Microsoft.EntityFrameworkCore;

namespace little_heart_bot_3.Services.Implements;

public class BotService : IBotService
{
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _options;
    private readonly IApiService _apiService;
    private readonly IUserService _userService;
    private readonly IDbContextFactory<LittleHeartDbContext> _dbContextFactory;

    public BotService(ILogger<BotHostedService> logger,
        JsonSerializerOptions options,
        [FromKeyedServices("bot:ApiService")] IApiService apiService,
        [FromKeyedServices("bot:UserService")] IUserService userService,
        IDbContextFactory<LittleHeartDbContext> dbContextFactory)
    {
        _logger = logger;
        _options = options;
        _apiService = apiService;
        _userService = userService;
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IEnumerable<JsonNode?>?> GetSessionListAsync(
        BotModel bot,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalResponse = await _apiService.GetNormalSessionListAsync(bot, cancellationToken);
            if ((int)normalResponse["code"]! != 0)
            {
                _logger.LogWithResponse(
                    () => _logger.LogError("获取普通的session_list失败"),
                    normalResponse.ToJsonString(_options));

                throw new LittleHeartException(Reason.RiskControl);
            }

            var normalSessionList = (JsonArray?)normalResponse["data"]!["session_list"];

            var blockedResponse = await _apiService.GetBlockedSessionListAsync(bot, cancellationToken);
            if ((int)blockedResponse["code"]! != 0)
            {
                _logger.LogWithResponse(
                    () => _logger.LogError("获取被屏蔽的session_list失败"),
                    blockedResponse.ToJsonString(_options));

                throw new LittleHeartException(Reason.RiskControl);
            }

            var blockedSessionList = (JsonArray?)blockedResponse["data"]!["session_list"];

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
            throw new LittleHeartException(ex.Message, ex, Reason.RiskControl);
        }
        catch (LittleHeartException)
        {
            throw;
        }
        catch (OperationCanceledException)
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

    public async Task<IEnumerable<JsonNode?>?> GetPrivateMessagesAsync(
        BotModel bot,
        UserModel user,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiService.GetPrivateMessagesAsync(bot, user, cancellationToken);
            int code = (int)response["code"]!;

            if (code != 0)
            {
                _logger.LogWithResponse(
                    () => _logger.LogError("获取 {uid} 的聊天记录失败", user.Uid),
                    response.ToJsonString(_options));
                throw new LittleHeartException(Reason.RiskControl);
            }

            return response["data"]!["messages"]?.AsArray().Reverse();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "获取uid {Uid} 的聊天记录时遇到 HttpRequestException 异常，重试多次后依然失败",
                user.Uid);
            throw new LittleHeartException(ex.Message, ex, Reason.RiskControl);
        }
        catch (LittleHeartException)
        {
            throw;
        }
        catch (OperationCanceledException)
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

    public async Task UpdateSignAsync(BotModel bot, string sign, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiService.UpdateSignAsync(bot, sign, cancellationToken);

            int code = (int)response["code"]!;
            switch (code)
            {
                case 0:
                    _logger.LogInformation("签名改为：{sign}", sign);
                    break;
                case -111:
                    _logger.LogWithResponse(
                        () => _logger.LogCritical("小心心bot的Cookie已过期"),
                        response.ToJsonString(_options));
                    throw new LittleHeartException(Reason.BotCookieExpired);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "小心心bot更新签名时出现 HttpRequestException 异常，重试多次后依然失败");
            throw new LittleHeartException(ex.Message, ex, Reason.RiskControl);
        }
        catch (LittleHeartException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "小心心bot更新签名时出现预料之外的错误");
        }
    }

    public async Task SendPrivateMessageAsync(
        BotModel bot,
        UserModel user,
        string content,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiService.SendPrivateMessageAsync(bot, user, content, cancellationToken);
            int code = (int)response["code"]!;
            if (code != 0)
            {
                _logger.LogWithResponse(
                    () => _logger.LogError("给uid {Uid} 发送私信失败", user.Uid),
                    response.ToJsonString(_options));
            }

            //没有抛异常就说明发送成功了
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            user.ConfigTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            user.ConfigNum++;
            db.Users.Update(user);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "给uid {Uid} 发送私信时出现 HttpRequestException 异常，重试多次后依然失败，私信的内容为:{Content}",
                user.Uid,
                content);
            throw new LittleHeartException(ex.Message, ex, Reason.RiskControl);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex,
                "给uid {Uid} 发送私信时出现预料之外的错误，私信的内容为:{Content}",
                user.Uid,
                content);
        }
    }


    public async Task HandleCommandAsync(
        BotModel bot,
        UserModel user,
        string command,
        string? parameter,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        try
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
        catch (LittleHeartException ex) when (ex.Reason == Reason.UserCookieExpired)
        {
            _logger.LogInformation("uid {Uid} 的cookie已过期", user.Uid);
            user.CookieStatus = CookieStatus.Error;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "处理uid {Uid} 的命令时遇到 HttpRequestException 异常，重试多次后依然失败",
                user.Uid);
            throw new LittleHeartException(Reason.RiskControl);
        }
        finally
        {
            db.Users.Update(user);
            await db.SaveChangesAsync(cancellationToken);
        }
    }


    #region CommandHandler

    /// <summary>
    /// 
    /// </summary>
    /// <param name="user"></param>
    /// <param name="parameter"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="LittleHeartException">
    /// <br/>Reason.RiskControl
    /// <br/>Reason.UserCookieExpired
    /// </exception>
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
        if (data?["live_room"] is null)
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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="user"></param>
    /// <param name="parameter"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="LittleHeartException">
    /// <br/>Reason.RiskControl
    /// <br/>Reason.UserCookieExpired
    /// </exception>
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
            if (parameter.Any(c => !char.IsAscii(c)))
            {
                throw new LittleHeartException(Reason.UserCookieExpired);
            }

            _ = new HttpRequestMessage()
            {
                Headers = { { "Cookie", parameter } }
            };

            user.Cookie = parameter.Replace("\n", "");
            user.Csrf = Globals.GetCsrf(user.Cookie);
            user.CookieStatus = CookieStatus.Unverified;
        }
        catch (Exception ex)
        {
            user.CookieStatus = CookieStatus.Error;
            _logger.LogWarning(ex, "uid {uid} 提交的cookie有误", user.Uid);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="user"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="LittleHeartException">
    /// <br/>Reason.RiskControl
    /// </exception>
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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="user"></param>
    /// <param name="parameter"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="LittleHeartException">
    /// <br/>Reason.RiskControl
    /// </exception>
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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="user"></param>
    /// <param name="parameter"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="LittleHeartException">
    /// <br/>Reason.RiskControl
    /// </exception>
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