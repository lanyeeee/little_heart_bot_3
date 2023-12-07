using System.Text.Json;
using System.Text.Json.Nodes;
using little_heart_bot_3.Data;
using little_heart_bot_3.Data.Models;
using little_heart_bot_3.Others;
using little_heart_bot_3.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;

namespace little_heart_bot_3;

public class Bot
{
    private readonly Logger _logger;
    private readonly IBotService _botService;
    private readonly IUserService _userService;
    private readonly LittleHeartDbContext _db;
    private readonly JsonSerializerOptions _options;
    private readonly HttpClient _httpClient;

    private bool _talking = true;
    private int _talkNum;
    private long _midnight; //今天0点的分钟时间戳
    private readonly BotModel _botModel;
    private Dictionary<long, UserModel> _users = new();

    public Bot([FromKeyedServices("bot:Logger")] Logger logger,
        [FromKeyedServices("bot:BotService")] IBotService botService,
        [FromKeyedServices("bot:UserService")] IUserService userService,
        LittleHeartDbContext db,
        JsonSerializerOptions options,
        HttpClient httpClient)
    {
        _logger = logger;
        _botService = botService;
        _userService = userService;
        _db = db;

        DateTimeOffset today = DateTime.Today;
        _midnight = today.ToUnixTimeSeconds();

        BotModel? botModel = _db.Bots.SingleOrDefault();
        if (botModel == null)
        {
            _logger.Error("数据库bot_table表中没有数据");
            throw new Exception("数据库bot_table表中没有数据，请自行添加");
        }

        _botModel = botModel;

        Globals.AppStatus = _botModel.AppStatus;
        Globals.SendStatus = _botModel.SendStatus;
        Globals.ReceiveStatus = _botModel.ReceiveStatus;

        _options = options;
        _httpClient = httpClient;
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
                await completedTask;
            }
            catch (LittleHeartException ex)
            {
                switch (ex.Reason)
                {
                    case Reason.Ban:
                        cancellationTokenSource.Cancel();

                        int cd = 15;
                        Globals.SendStatus = SendStatus.Cooling;
                        Globals.ReceiveStatus = ReceiveStatus.Cooling;
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

    private async Task CheckNewDayAsync()
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

        await foreach (var message in _db.Messages)
        {
            message.Code = 0;
            message.Response = null;
            message.Completed = false;
        }

        await foreach (var target in _db.Targets)
        {
            target.Exp = 0;
            target.WatchedSeconds = 0;
            target.Completed = false;
        }

        await foreach (var user in _db.Users)
        {
            user.Completed = false;
            user.ConfigNum = 0;
        }

        await _db.SaveChangesAsync(CancellationToken.None);
    }

    private async Task<JsonNode?> GetRoomDataAsync(UserModel user, long targetUid,
        CancellationToken cancellationToken = default)
    {
        var (imgKey, subKey) = await Wbi.GetWbiKeysAsync(_httpClient);
        Dictionary<string, string> signedParams = Wbi.EncWbi(
            parameters: new Dictionary<string, string> { { "mid", targetUid.ToString() } },
            imgKey: imgKey,
            subKey: subKey
        );

        string queryString = await new FormUrlEncodedContent(signedParams).ReadAsStringAsync(cancellationToken);

        HttpResponseMessage responseMessage = await _httpClient.SendAsync(new HttpRequestMessage
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
                user.Uid,
                targetUid);
            return null;
        }

        if (code != 0)
        {
            _logger.Error(new Exception(response.ToJsonString(_options)),
                "uid {uid} 获取 {targetUid} 的直播间数据失败",
                user.Uid,
                targetUid);
            throw new LittleHeartException(Reason.Ban);
        }

        return response["data"];
    }

    private string GetCsrf(string cookie)
    {
        return cookie.Substring(cookie.IndexOf("bili_jct=", StringComparison.Ordinal) + 9, 32);
    }

    private async Task SendMessageAsync(string content, UserModel user, CancellationToken cancellationToken = default)
    {
        _talking = await _botService.SendMessageAsync(_botModel, content, user, cancellationToken);
        if (_talking == false)
        {
            _logger.Warning("今日停止发送私信，今日共发送了 {talkNum} 条私信", _talkNum);
            return;
        }

        _talkNum++;

        user.ConfigTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        user.ConfigNum++;
        await _db.SaveChangesAsync(CancellationToken.None);
    }

    private async Task HandleCommandAsync(UserModel user, string command, string? parameter,
        CancellationToken cancellationToken = default)
    {
        switch (command)
        {
            case "/target_set" when parameter == null:
                return;
            case "/target_set":
            {
                string targetUidString = parameter;
                int targetCount = user.Targets.Count;

                if (!targetUidString.IsNumeric() || targetCount > 50)
                {
                    return;
                }

                long targetUid = long.Parse(targetUidString);

                JsonNode? data = await GetRoomDataAsync(user, targetUid, cancellationToken);
                if (data == null)
                {
                    return;
                }

                string targetName = (string)data["name"]!;
                long roomId = (long)data["live_room"]!["roomid"]!;
                TargetModel? target = user.Targets.FirstOrDefault(t => t.TargetUid == targetUid);

                if (target != null)
                {
                    target.TargetName = targetName;
                    target.RoomId = roomId;
                    target.Completed = false;

                    await _db.SaveChangesAsync(CancellationToken.None);
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

                    await _db.Targets.AddAsync(target, CancellationToken.None);
                    await _db.SaveChangesAsync(CancellationToken.None);

                    var message = user.Messages.FirstOrDefault(m => m.TargetUid == targetUid);

                    if (message != null)
                    {
                        message.Completed = false;
                        await _db.SaveChangesAsync(CancellationToken.None);
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
                        await _db.Messages.AddAsync(message, CancellationToken.None);
                        await _db.SaveChangesAsync(CancellationToken.None);
                    }
                }

                user.Completed = false;
                await _db.SaveChangesAsync(CancellationToken.None);
                break;
            }
            case "/target_delete" when parameter == null:
                return;
            case "/target_delete" when parameter == "all":
            {
                _db.Targets.RemoveRange(user.Targets);
                await _db.SaveChangesAsync(CancellationToken.None);
                return;
            }
            case "/target_delete":
            {
                string targetUidString = parameter;
                if (!targetUidString.IsNumeric())
                {
                    return;
                }

                long targetUid = long.Parse(targetUidString);
                TargetModel? target = user.Targets.FirstOrDefault(t => t.TargetUid == targetUid);
                if (target != null)
                {
                    user.Targets.Remove(target);
                    await _db.SaveChangesAsync(CancellationToken.None);
                }

                break;
            }
            case "/message_set" when parameter == null:
                return;
            case "/message_set":
            {
                string[] pair = parameter.Split(" ", 2);
                if (pair.Length != 2)
                {
                    return;
                }

                string targetUidString = pair[0].Trim();
                string content = pair[1].Trim();
                int messageCount = user.Messages.Count;
                if (!targetUidString.IsNumeric() || content.Length > 20 || messageCount > 50)
                {
                    return;
                }

                long targetUid = long.Parse(targetUidString);

                MessageModel? message = user.Messages.FirstOrDefault(m => m.TargetUid == targetUid);
                if (message != null)
                {
                    message.Content = content;
                    message.Completed = false;
                    message.Code = 0;
                    message.Response = null;
                    await _db.SaveChangesAsync(CancellationToken.None);
                }
                else
                {
                    JsonNode? data = await GetRoomDataAsync(user, targetUid, cancellationToken);
                    if (data == null)
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
                    await _db.Messages.AddAsync(message, CancellationToken.None);
                    await _db.SaveChangesAsync(CancellationToken.None);
                }

                break;
            }
            case "/message_delete" when parameter == null:
                return;
            case "/message_delete" when parameter == "all":
            {
                //需要保留的message(有对应target的message)
                var updatedMessage = user.Messages.Where(
                    m => user.Targets.Any(t => t.TargetUid == m.TargetUid)).ToList();
                foreach (var message in updatedMessage)
                {
                    message.Content = Globals.DefaultMessageContent;
                    message.Code = 0;
                    message.Response = null;
                }

                await _db.SaveChangesAsync(CancellationToken.None);

                //需要删除的message(除了要保留的message，剩下的都是需要删除的message)
                var removedMessages = user.Messages.Except(updatedMessage).ToList();
                _db.Messages.RemoveRange(removedMessages);
                await _db.SaveChangesAsync(CancellationToken.None);

                return;
            }
            case "/message_delete":
            {
                string targetUidString = parameter;
                if (!targetUidString.IsNumeric())
                {
                    return;
                }

                long targetUid = long.Parse(targetUidString);

                MessageModel? message = user.Messages.FirstOrDefault(m => m.TargetUid == targetUid);
                if (message is null)
                {
                    break;
                }

                TargetModel? target = user.Targets.FirstOrDefault(t => t.TargetUid == targetUid);

                if (target != null)
                {
                    message.Content = Globals.DefaultMessageContent;
                    message.Code = 0;
                    message.Response = null;
                    await _db.SaveChangesAsync(CancellationToken.None);
                }
                else
                {
                    _db.Messages.Remove(message);
                    await _db.SaveChangesAsync(CancellationToken.None);
                }

                break;
            }
            case "/cookie_commit" when parameter is null:
                break;
            case "/cookie_commit":
            {
                try
                {
                    user.Cookie = parameter.Replace("\n", "");
                    user.Csrf = GetCsrf(user.Cookie);
                    user.CookieStatus = 0;
                    await _db.SaveChangesAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
#if DEBUG
                    Console.WriteLine(ex);
#endif
                    _logger.Error(ex, "uid {uid} 提交的cookie有误", user.Uid);
                }

                break;
            }
            case "/config_all":
            {
                string? content = _userService.GetConfigAllString(user);
                if (content == null)
                {
                    return;
                }

                await SendMessageAsync(content, user, cancellationToken);
                break;
            }
            case "/message_config" when parameter == null:
            {
                string? content = _userService.GetAllMessageConfigString(user);
                if (content == null)
                {
                    return;
                }

                if (content.Length > 450)
                {
                    content =
                        "设置的弹幕过多，配置信息长度大于500，超过了私信长度的上限，无法发送\n\n请尝试使用\n/message_config 目标uid\n进行单个查询\n\n或者使用/message_config all\n获取分段的完整配置信息(每段消耗一次查询次数)";
                }

                content += $"\n已用查询次数({user.ConfigNum + 1}/10)\n";
                await SendMessageAsync(content, user, cancellationToken);
                break;
            }
            case "/message_config":
            {
                parameter = parameter.Trim();
                if (parameter == "all")
                {
                    List<string>? contents = _userService.GetAllMessageConfigStringSplit(user);
                    if (contents == null)
                    {
                        return;
                    }

                    foreach (string message in contents)
                    {
                        string content = message + $"\n已用查询次数({user.ConfigNum + 1}/10)\n";
                        await SendMessageAsync(content, user, cancellationToken);
                        await Task.Delay(1000, cancellationToken);
                    }
                }
                else
                {
                    long targetUid = long.Parse(parameter);
                    MessageModel? message = user.Messages.FirstOrDefault(m => m.TargetUid == targetUid);
                    if (message == null)
                    {
                        return;
                    }

                    string? content = _userService.GetSpecifyMessageConfigString(message);
                    if (content == null)
                    {
                        return;
                    }

                    content += $"\n已用查询次数({user.ConfigNum + 1}/10)\n";
                    await SendMessageAsync(content, user, cancellationToken);
                }

                break;
            }
            case "/target_config" when parameter == null:
            {
                string? content = _userService.GetAllTargetConfigString(user);
                if (content == null)
                {
                    return;
                }

                if (content.Length > 450)
                {
                    content =
                        "设置的目标过多，配置信息长度大于500，超过了私信长度的上限，无法发送\n\n请尝试使用\n/target_config 目标uid\n进行单个查询\n\n或者使用\n/target_config all\n获取分段的完整配置信息(每段消耗一次查询次数)\n";
                }

                content += $"\n已用查询次数({user.ConfigNum + 1}/10)\n";
                await SendMessageAsync(content, user, cancellationToken);
                break;
            }
            case "/target_config":
            {
                parameter = parameter.Trim();
                if (parameter == "all")
                {
                    List<string>? contents = _userService.GetAllTargetConfigStringSplit(user);
                    if (contents == null)
                    {
                        return;
                    }

                    foreach (string message in contents)
                    {
                        string content = message + $"\n已用查询次数({user.ConfigNum + 1}/10)\n";
                        await SendMessageAsync(content, user, cancellationToken);
                        await Task.Delay(1000, cancellationToken);
                    }
                }
                else
                {
                    long targetUid = long.Parse(parameter);
                    TargetModel? target = user.Targets.FirstOrDefault(t => t.TargetUid == targetUid);
                    if (target == null)
                    {
                        return;
                    }

                    string? content = _userService.GetSpecifyTargetConfigString(target);
                    if (content == null)
                    {
                        return;
                    }

                    content += $"\n已用查询次数({user.ConfigNum + 1}/10)\n";
                    await SendMessageAsync(content, user, cancellationToken);
                }

                break;
            }
            case "/delete":
            {
                user.Cookie = "";
                user.Csrf = "";
                user.Completed = false;
                user.CookieStatus = CookieStatus.Error;

                _db.Targets.RemoveRange(user.Targets);
                _db.Messages.RemoveRange(user.Messages);

                await _db.SaveChangesAsync(CancellationToken.None);
                break;
            }
        }
    }

    /// <summary>
    /// 处理lastTimestamp之后的私信
    /// </summary>
    /// <param name="user"></param>
    /// <param name="lastTimestamp"></param>
    /// <param name="messages"></param>
    /// <param name="cancellationToken"></param>
    private async Task HandleMessagesAsync(UserModel user, long lastTimestamp, IEnumerable<JsonNode?> messages,
        CancellationToken cancellationToken = default)
    {
        foreach (var msg in messages)
        {
            if (msg == null)
            {
                continue;
            }

            //忽略 已读的、bot发送的、非文字的 消息
            if ((long?)msg["timestamp"] <= lastTimestamp ||
                (long?)msg["sender_uid"] == _botModel.Uid ||
                (int?)msg["msg_type"] != 1)
            {
                continue;
            }

            try
            {
                long? timestamp = (long?)msg["timestamp"];
                string? contentJson = (string?)msg["content"];
                if (timestamp == null || contentJson == null)
                {
                    return;
                }

                user.ReadTimestamp = timestamp.Value;
                string? content = (string?)JsonNode.Parse(contentJson)!["content"];
                content = content?.Trim();
                _logger.Information("{Uid}：{Content}", user.Uid, content);
#if DEBUG
                Console.WriteLine($"{user.Uid}：{content}");
#endif
                if (content?.StartsWith("/") ?? false)
                {
                    string[] pair = content.Split(" ", 2);
                    if (pair.Length == 2)
                    {
                        string command = pair[0].Trim();
                        string parameter = pair[1].Trim();
                        await HandleCommandAsync(user, command, parameter, cancellationToken);
                    }
                    else
                    {
                        string command = pair[0].Trim();
                        await HandleCommandAsync(user, command, null, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, "处理uid {Uid} 的消息时出错", user.Uid);
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

        _users = await _db.Users
            .Include(u => u.Messages)
            .Include(u => u.Targets)
            .AsSplitQuery()
            .ToDictionaryAsync(user => user.Uid, user => user, cancellationToken);

        foreach (var session in sessionList)
        {
            if (session == null)
            {
                continue;
            }

            long? nullableUid = (long?)session["talker_id"];
            if (nullableUid == null)
            {
                continue;
            }

            long uid = nullableUid.Value;

            JsonObject? lastMsg = session["last_msg"]?.AsObject();
            if (lastMsg == null)
            {
                continue;
            }

            long? timestamp = lastMsg.Count != 0 ? (long?)lastMsg["timestamp"] : 0;
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
                    Cookie = string.Empty,
                    Csrf = string.Empty,
                    ReadTimestamp = timestamp.Value,
                    ConfigTimestamp = 0,
                    ConfigNum = 0
                };
                IEnumerable<JsonNode?>? messages =
                    await _botService.GetMessagesAsync(_botModel, user, cancellationToken);

                _users.Add(uid, user);
                await _db.Users.AddAsync(user, CancellationToken.None);
                await _db.SaveChangesAsync(CancellationToken.None);

                if (messages != null)
                {
                    await HandleMessagesAsync(user, 0, messages, cancellationToken);
                }
            }
            else if (timestamp > user.ReadTimestamp) //发新消息的用户
            {
                IEnumerable<JsonNode?>? messages =
                    await _botService.GetMessagesAsync(_botModel, user, cancellationToken);

                //只要成功获取用户的私信，无论这些私信是否成功处理，都只处理一次
                long readTimestamp = user.ReadTimestamp;
                user.ReadTimestamp = timestamp.Value;
                await _db.SaveChangesAsync(CancellationToken.None);

                if (messages != null)
                {
                    await HandleMessagesAsync(user, readTimestamp, messages, cancellationToken);
                }
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
            await CheckNewDayAsync();
            await HandleIncomingMessageAsync(cancellationToken);

            Globals.ReceiveStatus = ReceiveStatus.Normal;
            Globals.SendStatus = _talking ? SendStatus.Normal : SendStatus.Forbidden;

            await Task.Delay(1000, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }
}