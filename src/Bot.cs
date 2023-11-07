using System.Text.Json;
using System.Text.Json.Nodes;
using little_heart_bot_3.Models;
using little_heart_bot_3.Others;
using little_heart_bot_3.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;

namespace little_heart_bot_3;

public class Bot
{
    private readonly Logger _logger;
    private readonly IBotService _botService;
    private readonly IMessageService _messageService;
    private readonly ITargetService _targetService;
    private readonly IUserService _userService;

    private bool _talking = true;
    private int _talkNum;
    private long _midnight; //今天0点的分钟时间戳
    private readonly BotModel _botModel;
    private Dictionary<string, UserModel> _users = new();
    private readonly JsonSerializerOptions _options;

    public Bot([FromKeyedServices("bot:Logger")] Logger logger,
        [FromKeyedServices("bot:BotService")] IBotService botService,
        [FromKeyedServices("bot:MessageService")]
        IMessageService messageService,
        [FromKeyedServices("bot:TargetService")]
        ITargetService targetService,
        [FromKeyedServices("bot:UserService")] IUserService userService)
    {
        _logger = logger;
        _botService = botService;
        _messageService = messageService;
        _targetService = targetService;
        _userService = userService;

        DateTimeOffset today = DateTime.Today;
        _midnight = today.ToUnixTimeSeconds();

        BotModel? botModel = _botService.GetBotAsync().Result;
        if (botModel == null)
        {
            _logger.Error("数据库bot_table表中没有数据");
            throw new Exception("数据库bot_table表中没有数据，请自行添加");
        }

        _botModel = botModel;

        Globals.AppStatus = _botModel.AppStatus;
        Globals.SendStatus = _botModel.SendStatus;
        Globals.ReceiveStatus = _botModel.ReceiveStatus;

        _options = Globals.JsonSerializerOptions;
    }

    public async Task Main()
    {
        while (true)
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            try
            {
                Task updateSignTask = UpdateSignMain(cancellationTokenSource.Token);
                Task handleMessageTask = HandleMessageMain(cancellationTokenSource.Token);

                //这两个task是死循环，如果结束了只有可能是抛了Ban或者CookieExpired的异常
                var completedTask = await Task.WhenAny(updateSignTask, handleMessageTask);
                throw completedTask.Exception!;
            }
            catch (LittleHeartException ex)
            {
                switch (ex.Reason)
                {
                    case Reason.Ban:
                        cancellationTokenSource.Cancel();

                        int cd = 15;
                        Globals.SendStatus = -1;
                        Globals.ReceiveStatus = -1;
                        while (cd != 0)
                        {
                            _logger.Warning("遇到风控 还需冷却 {cd} 分钟", cd);
                            await Task.Delay(60 * 1000, CancellationToken.None);
                            cd--;
                        }

                        break;
                    case Reason.CookieExpired:
                        //TODO: 目前如果小心心bot的cookie过期，直接结束bot的task，后续要支持cookie热更新
                        return;
                    default:
                        _logger.Fatal(ex, "这种情况不应该发生，如果发生了就是代码编写有问题");
                        break;
                }
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, "遇到了意料之外的错误");
            }
            finally
            {
                cancellationTokenSource.Cancel();
            }
        }
    }

    private async Task CheckNewDayAsync(CancellationToken cancellationToken = default)
    {
        if (DateTimeOffset.Now.ToUnixTimeSeconds() - _midnight < 24 * 60 * 60 + 3 * 60)
        {
            return;
        }

        //新的一天要把一些数据重置
        _talking = true;
        _talkNum = 0;
        DateTimeOffset today = DateTime.Today;
        _midnight = today.ToUnixTimeSeconds();

        await _messageService.NewDayAsync(cancellationToken);
        await _targetService.NewDayAsync(cancellationToken);
        await _userService.NewDay(cancellationToken);
    }

    private async Task<Dictionary<string, UserModel>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, UserModel>();
        var users = await _userService.GetAll(cancellationToken);
        users.ForEach(user => result.Add(user.Uid!, user));
        return result;
    }

    private async Task<JsonNode?> GetRoomDataAsync(string uid, string targetUid,
        CancellationToken cancellationToken = default)
    {
        UserModel? userEntity = await _userService.Get(uid, cancellationToken);
        if (userEntity == null)
        {
            _logger.Error("找不到uid为 {uid} 的用户", uid);
            return null;
        }

        var (imgKey, subKey) = await Wbi.GetWbiKeysAsync();
        Dictionary<string, string> signedParams = Wbi.EncWbi(
            parameters: new Dictionary<string, string> { { "mid", targetUid } },
            imgKey: imgKey,
            subKey: subKey
        );

        string queryString = await new FormUrlEncodedContent(signedParams).ReadAsStringAsync(cancellationToken);

        HttpResponseMessage responseMessage = await Globals.HttpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri($"https://api.bilibili.com/x/space/wbi/acc/info?{queryString}"),
            Headers =
            {
                {
                    "user-agent",
                    "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/102.0.0.0 Safari/537.36"
                },
                { "cookie", userEntity.Cookie }
            },
        }, cancellationToken);
        await Task.Delay(1000, cancellationToken);

        JsonNode? response = JsonNode.Parse(await responseMessage.Content.ReadAsStringAsync(cancellationToken));
        if (response == null)
        {
            throw new LittleHeartException(Reason.NullResponse);
        }

        int code = (int)response["code"]!;

        if (code is -400 or -404)
        {
            _logger.Error(new Exception(response.ToJsonString(_options)),
                "uid {uid} 获取 {targetUid} 的直播间数据失败",
                uid,
                targetUid);
            return null;
        }

        if (code != 0)
        {
            _logger.Error(new Exception(response.ToJsonString(_options)),
                "uid {uid} 获取 {targetUid} 的直播间数据失败",
                uid,
                targetUid);
            throw new LittleHeartException(Reason.Ban);
        }

        return response["data"];
    }

    private string GetCsrf(string cookie)
    {
        return cookie.Substring(cookie.IndexOf("bili_jct=", StringComparison.Ordinal) + 9, 32);
    }

    private async Task SendMessageAsync(string content, string userUid, CancellationToken cancellationToken = default)
    {
        _talking = await _botService.SendMessageAsync(_botModel, content, userUid, cancellationToken);
        if (_talking == false)
        {
            _logger.Warning("今日停止发送私信，今日共发送了 {talkNum} 条私信", _talkNum);
            return;
        }

        _talkNum++;
        _users[userUid].ConfigTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
        _users[userUid].ConfigNum++;
        await _userService.Update(_users[userUid], cancellationToken);
    }

    private async Task HandleCommandAsync(string uid, string command, string? parameter,
        CancellationToken cancellationToken = default)
    {
        if (command == "/target_set")
        {
            if (parameter == null)
            {
                return;
            }

            string targetUid = parameter;
            int targetNum = await _targetService.GetTargetNumAsync(uid, cancellationToken);

            if (!targetUid.IsNumeric() || targetNum > 50)
            {
                return;
            }

            JsonNode? data = await GetRoomDataAsync(uid, targetUid, cancellationToken);
            if (data == null)
            {
                return;
            }

            string? targetName = (string?)data["name"];
            string? roomId = data["live_room"]!["roomid"]?.GetValue<long>().ToString();
            bool targetExist = await _targetService.CheckExistByUidAndTargetUidAsync(uid, targetUid, cancellationToken);

            if (targetExist)
            {
                await _targetService.SetTargetNameAndRoomIdByUidAndTargetUidAsync(targetName, roomId, uid,
                    targetUid, cancellationToken);
                await _messageService.SetCompletedByUidAndTargetUid(0, uid, targetUid, cancellationToken);
            }
            else
            {
                var targetEntity = new TargetModel()
                {
                    Uid = uid,
                    Completed = 0,
                    RoomId = roomId,
                    TargetName = targetName,
                    TargetUid = targetUid
                };
                await _targetService.InsertAsync(targetEntity, cancellationToken);

                bool messageExist =
                    await _messageService.CheckExistByUidAndTargetUid(uid, targetUid, cancellationToken);
                if (messageExist)
                {
                    await _messageService.SetCompletedByUidAndTargetUid(0, uid, targetUid, cancellationToken);
                }
                else
                {
                    var message = new MessageModel()
                    {
                        Uid = uid,
                        TargetName = targetName,
                        TargetUid = targetUid,
                        RoomId = roomId,
                        Code = 0,
                        Content = "飘过~",
                        Completed = 0
                    };
                    await _messageService.Insert(message, cancellationToken);
                }
            }

            await _userService.SetCompleted(0, uid, cancellationToken);
        }
        else if (command == "/target_delete")
        {
            if (parameter == null)
            {
                return;
            }

            if (parameter == "all")
            {
                await _targetService.DeleteByUidAsync(uid, cancellationToken);
                return;
            }

            string targetUid = parameter;
            if (!targetUid.IsNumeric())
            {
                return;
            }

            await _targetService.DeleteByUidAndTargetUidAsync(uid, targetUid, cancellationToken);
        }
        else if (command == "/message_set")
        {
            if (parameter == null)
            {
                return;
            }

            string[] pair = parameter.Split(" ", 2);
            if (pair.Length != 2)
            {
                return;
            }

            string targetUid = pair[0].Trim();
            string content = pair[1].Trim();
            int messageNum = await _messageService.GetMessageNum(uid, cancellationToken);
            if (!targetUid.IsNumeric() || content.Length > 20 || messageNum > 50)
            {
                return;
            }

            bool exist = await _messageService.CheckExistByUidAndTargetUid(uid, targetUid, cancellationToken);
            if (exist)
            {
                await _messageService.SetContentByUidAndTargetUid(content, uid, targetUid, cancellationToken);
                await _messageService.SetCompletedByUidAndTargetUid(0, uid, targetUid, cancellationToken);
                await _messageService.SetCodeAndResponseByUidAndTargetUid(0, null, uid, targetUid, cancellationToken);
            }
            else
            {
                JsonNode? data = await GetRoomDataAsync(uid, targetUid, cancellationToken);
                if (data == null)
                {
                    return;
                }

                string? targetName = (string?)data["name"];
                string? roomId = (string?)data["live_room"]!["roomid"];

                var messageEntity = new MessageModel
                {
                    Uid = uid,
                    TargetUid = targetUid,
                    TargetName = targetName,
                    RoomId = roomId,
                    Content = content,
                    Code = 0
                };

                await _messageService.Insert(messageEntity, cancellationToken);
            }
        }
        else if (command == "/message_delete")
        {
            if (parameter == null)
            {
                return;
            }

            if (parameter == "all")
            {
                List<TargetModel>? targets = _users[uid].Targets;
                if (targets == null)
                {
                    return;
                }

                foreach (var target in targets)
                {
                    bool exist =
                        await _targetService.CheckExistByUidAndTargetUidAsync(uid, target.TargetUid, cancellationToken);
                    if (exist)
                    {
                        await _messageService.SetContentByUidAndTargetUid("飘过~", uid, target.TargetUid,
                            cancellationToken);
                        await _messageService.SetCodeAndResponseByUidAndTargetUid(0, null, uid,
                            target.TargetUid, cancellationToken);
                    }
                    else
                    {
                        await _messageService.DeleteByUidAndTargetUid(uid, target.Uid, cancellationToken);
                    }
                }

                return;
            }

            string targetUid = parameter;
            if (!targetUid.IsNumeric())
            {
                return;
            }

            bool targetExist = await _targetService.CheckExistByUidAndTargetUidAsync(uid, targetUid, cancellationToken);
            if (targetExist)
            {
                await _messageService.SetContentByUidAndTargetUid("飘过~", uid, targetUid, cancellationToken);
                await _messageService.SetCodeAndResponseByUidAndTargetUid(0, null, uid, targetUid, cancellationToken);
            }
            else
            {
                await _messageService.DeleteByUidAndTargetUid(uid, targetUid, cancellationToken);
            }
        }
        else if (command == "/cookie_commit")
        {
            try
            {
                if (parameter == null)
                {
                    return;
                }

                var userEntity = await _userService.Get(uid, cancellationToken);
                if (userEntity == null)
                {
                    return;
                }

                userEntity.Cookie = parameter.Replace("\n", "");
                userEntity.Csrf = GetCsrf(userEntity.Cookie);
                userEntity.CookieStatus = 0;

                await _userService.Update(userEntity, cancellationToken);
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine(ex);
#endif
                _logger.Error(ex, "uid {uid} 提交的cookie有误", uid);
            }
        }
        else if (command == "/config_all")
        {
            string? content = _userService.GetConfigString(_users[uid]);
            if (content == null)
            {
                return;
            }

            await SendMessageAsync(content, uid, cancellationToken);
        }
        else if (command == "/message_config")
        {
            if (parameter == null)
            {
                string? content = _userService.GetMessageConfigString(_users[uid]);
                if (content == null)
                {
                    return;
                }

                if (content.Length > 450)
                {
                    content =
                        "设置的弹幕过多，配置信息长度大于500，超过了私信长度的上限，无法发送\n\n请尝试使用\n/message_config 目标uid\n进行单个查询\n\n或者使用/message_config all\n获取分段的完整配置信息(每段消耗一次查询次数)";
                }

                content += $"\n已用查询次数({_users[uid].ConfigNum + 1}/10)\n";
                await SendMessageAsync(content, uid, cancellationToken);
            }
            else
            {
                parameter = parameter.Trim();
                if (parameter == "all")
                {
                    List<string>? contents = _userService.GetMessageConfigStringSplit(_users[uid]);
                    if (contents == null)
                    {
                        return;
                    }

                    foreach (string message in contents)
                    {
                        string content = message + $"\n已用查询次数({_users[uid].ConfigNum + 1}/10)\n";
                        await SendMessageAsync(content, uid, cancellationToken);
                        await Task.Delay(1000, cancellationToken);
                    }
                }
                else
                {
                    string? content =
                        await _userService.GetMessageConfigStringAsync(_users[uid], parameter.Trim(),
                            cancellationToken);
                    if (content == null)
                    {
                        return;
                    }

                    content += $"\n已用查询次数({_users[uid].ConfigNum + 1}/10)\n";
                    await SendMessageAsync(content, uid, cancellationToken);
                }
            }
        }
        else if (command == "/target_config")
        {
            if (parameter == null)
            {
                string? content = _userService.GetTargetConfigString(_users[uid]);
                if (content == null)
                {
                    return;
                }

                if (content.Length > 450)
                {
                    content =
                        "设置的目标过多，配置信息长度大于500，超过了私信长度的上限，无法发送\n\n请尝试使用\n/target_config 目标uid\n进行单个查询\n\n或者使用\n/target_config all\n获取分段的完整配置信息(每段消耗一次查询次数)\n";
                }

                content += $"\n已用查询次数({_users[uid].ConfigNum + 1}/10)\n";
                await SendMessageAsync(content, uid, cancellationToken);
            }
            else
            {
                parameter = parameter.Trim();
                if (parameter == "all")
                {
                    List<string>? contents = _userService.GetTargetConfigStringSplit(_users[uid]);
                    if (contents == null)
                    {
                        return;
                    }

                    foreach (string message in contents)
                    {
                        string content = message + $"\n已用查询次数({_users[uid].ConfigNum + 1}/10)\n";
                        await SendMessageAsync(content, uid, cancellationToken);
                        await Task.Delay(1000, cancellationToken);
                    }
                }
                else
                {
                    string? content =
                        await _userService.GetTargetConfigStringAsync(_users[uid], parameter.Trim(), cancellationToken);
                    if (content == null)
                    {
                        return;
                    }

                    content += $"\n已用查询次数({_users[uid].ConfigNum + 1}/10)\n";
                    await SendMessageAsync(content, uid, cancellationToken);
                }
            }
        }
        else if (command == "/delete")
        {
            await _targetService.DeleteByUidAsync(uid, cancellationToken);
            await _messageService.DeleteByUid(uid, cancellationToken);

            var userEntity = await _userService.Get(uid, cancellationToken);
            if (userEntity == null)
            {
                return;
            }

            userEntity.Cookie = "";
            userEntity.Csrf = "";
            userEntity.Completed = 0;
            userEntity.CookieStatus = -1;

            await _userService.Update(userEntity, cancellationToken);
            _users[uid] = userEntity;
        }
    }

    private async Task HandleMessagesAsync(string uid, int lastTimestamp, IEnumerable<JsonNode?>? messages,
        CancellationToken cancellationToken = default)
    {
        if (messages == null)
        {
            return;
        }

        foreach (var msg in messages)
        {
            if (msg == null)
            {
                continue;
            }

            //忽略 已读的、bot发送的、非文字的 消息
            if ((int?)msg["timestamp"] <= lastTimestamp ||
                msg["sender_uid"]?.GetValue<long>().ToString() == _botModel.Uid ||
                (int?)msg["msg_type"] != 1)
            {
                continue;
            }

            try
            {
                string? timestamp = msg["timestamp"]?.GetValue<long>().ToString();
                string? contentJson = (string?)msg["content"];
                if (timestamp == null || contentJson == null)
                {
                    return;
                }

                _users[uid].ReadTimestamp = timestamp;
                string? content = (string?)JsonNode.Parse(contentJson)!["content"];
                content = content?.Trim();
                _logger.Information("{Uid}：{Content}", uid, content);
#if DEBUG
                Console.WriteLine($"{uid}：{content}");
#endif
                if (content?.StartsWith("/") ?? false)
                {
                    string[] pair = content.Split(" ", 2);
                    if (pair.Length == 2)
                    {
                        string command = pair[0].Trim();
                        string parameter = pair[1].Trim();
                        await HandleCommandAsync(uid, command, parameter, cancellationToken);
                    }
                    else
                    {
                        string command = pair[0].Trim();
                        await HandleCommandAsync(uid, command, null, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, "处理uid {Uid} 的消息时出错", uid);
            }
        }
    }

    private async Task HandleIncomingMessageAsync(CancellationToken cancellationToken = default)
    {
        IEnumerable<JsonNode?>? sessionList = await _botService.GetSessionListAsync(_botModel, cancellationToken);
        if (sessionList == null)
        {
            return;
        }

        _users = await GetUsersAsync(cancellationToken);

        foreach (var session in sessionList)
        {
            if (session == null)
            {
                continue;
            }

            string? uid = session["talker_id"]?.GetValue<long>().ToString();
            if (uid == null)
            {
                continue;
            }

            JsonObject? lastMsg = session["last_msg"]?.AsObject();
            if (lastMsg == null)
            {
                continue;
            }

            int? timestamp = lastMsg.Count != 0 ? (int?)lastMsg["timestamp"] : 0;
            if (timestamp == null)
            {
                continue;
            }

            _users.TryGetValue(uid, out var user);

            if (user == null) //新用户
            {
                user = new UserModel
                {
                    Uid = uid,
                    Cookie = "",
                    Csrf = "",
                    ReadTimestamp = timestamp.ToString(),
                    ConfigTimestamp = "0",
                    ConfigNum = 0
                };
                IEnumerable<JsonNode?>? messages =
                    await _botService.GetMessagesAsync(_botModel, user, cancellationToken);
                _users.Add(uid, user);
                await _userService.Insert(user, cancellationToken);
                await HandleMessagesAsync(uid, 0, messages, cancellationToken);
            }
            else if (timestamp > Int32.Parse(_users[uid].ReadTimestamp!)) //发新消息的用户
            {
                int readTimestamp = Int32.Parse(_users[uid].ReadTimestamp!);
                IEnumerable<JsonNode?>? messages =
                    await _botService.GetMessagesAsync(_botModel, user, cancellationToken);
                await _userService.SetReadTimestamp(timestamp.ToString()!, uid, cancellationToken);
                await HandleMessagesAsync(uid, readTimestamp, messages, cancellationToken);
            }
        }
    }

    private async Task UpdateSignMain(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await _botService.UpdateSignAsync(_botModel, cancellationToken);
            await Task.Delay(1000, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task HandleMessageMain(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await CheckNewDayAsync(cancellationToken);
            await HandleIncomingMessageAsync(cancellationToken);

            Globals.ReceiveStatus = 0;
            if (_talking)
            {
                Globals.SendStatus = 0;
            }
            else
            {
                Globals.SendStatus = -2;
            }

            await Task.Delay(1000, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }
}