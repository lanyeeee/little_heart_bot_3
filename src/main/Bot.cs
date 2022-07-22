using little_heart_bot_3.entity;
using little_heart_bot_3.others;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace little_heart_bot_3.main;

public class Bot
{
    private static Bot? _instance;
    public static Bot Instance => _instance ?? new Bot();

    private readonly Logger _logger;
    private bool _talking = true;
    private int _talkNum;
    private long _midnight; //今天0点的分钟时间戳
    private readonly BotEntity _botEntity;
    private Dictionary<string, UserEntity> _users = new();


    private Bot()
    {
        _instance = this;

        DateTimeOffset today = DateTime.Today;
        _midnight = today.ToUnixTimeSeconds();

        _logger = new Logger("bot");

        _botEntity = Globals.BotRepository.GetBot()!;
        Globals.AppStatus = _botEntity.AppStatus;
        Globals.SendStatus = _botEntity.SendStatus;
        Globals.ReceiveStatus = _botEntity.ReceiveStatus;
    }

    private async Task CheckNewDay()
    {
        if (DateTimeOffset.Now.ToUnixTimeSeconds() - _midnight < 24 * 60 * 60 + 3 * 60) return;

        //新的一天要把一些数据重置
        _talking = true;
        _talkNum = 0;
        DateTimeOffset today = DateTime.Today;
        _midnight = today.ToUnixTimeSeconds();

        await Globals.MessageRepository.NewDay();
        await Globals.TargetRepository.NewDay();
        await Globals.UserRepository.NewDay();
    }

    private async Task<Dictionary<string, UserEntity>> GetUsers()
    {
        var result = new Dictionary<string, UserEntity>();
        var users = await Globals.UserRepository.GetAll();
        users.ForEach(user => { result.Add(user.Uid!, user); });
        return result;
    }

    private async Task<JToken?> GetRoomData(string uid, string targetUid)
    {
        HttpResponseMessage responseMessage = await Globals.HttpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri($"https://api.bilibili.com/x/space/acc/info?mid={targetUid}")
        });
        await Task.Delay(1000);

        JObject response = JObject.Parse(await responseMessage.Content.ReadAsStringAsync());
        int? code = (int?)response["code"];

        if (code == -400) return null;

        if (code != 0)
        {
            await _logger.Log(response);
            await _logger.Log($"uid {uid} 获取 {targetUid} 的直播间数据失败");
            throw new ApiException();
        }

        return response["data"];
    }

    private string GetCsrf(string cookie)
    {
        return cookie.Substring(cookie.IndexOf("bili_jct=", StringComparison.Ordinal) + 9, 32);
    }

    private async Task SendMessage(string content, string uid)
    {
        _talking = await _botEntity.SendMessage(content, uid, _logger);
        if (_talking == false)
        {
            await _logger.Log($"今日私信发送数量已达上限，共发送了 {_talkNum} 条私信");
            return;
        }

        _talkNum++;
        _users[uid].ConfigTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
        _users[uid].ConfigNum++;
        await Globals.UserRepository.Update(_users[uid]);
    }

    private async Task HandleCommand(string uid, string command, string? parameter)
    {
        if (command == "/target_set")
        {
            if (parameter == null) return;
            string targetUid = parameter;
            int targetNum = await Globals.TargetRepository.GetTargetNum(uid);

            if (!targetUid.IsNumeric() || targetNum > 10) return;

            JToken? data = await GetRoomData(uid, targetUid);
            if (data == null) return;

            string? targetName = (string?)data["name"];
            string? roomId = (string?)data["live_room"]!["roomid"];
            bool targetExist = await Globals.TargetRepository.CheckExistByUidAndTargetUid(uid, targetUid);

            if (targetExist)
            {
                await Globals.TargetRepository.SetTargetNameAndRoomIdByUidAndTargetUid(targetName, roomId, uid,
                    targetUid);
                await Globals.MessageRepository.SetCompletedByUidAndTargetUid(0, uid, targetUid);
            }
            else
            {
                var targetEntity = new TargetEntity()
                {
                    Uid = uid,
                    Completed = 0,
                    RoomId = roomId,
                    TargetName = targetName,
                    TargetUid = targetUid
                };
                await Globals.TargetRepository.Insert(targetEntity);

                bool messageExist = await Globals.MessageRepository.CheckExistByUidAndTargetUid(uid, targetUid);
                if (messageExist)
                {
                    await Globals.MessageRepository.SetCompletedByUidAndTargetUid(0, uid, targetUid);
                }
                else
                {
                    var message = new MessageEntity()
                    {
                        Uid = uid,
                        TargetName = targetName,
                        TargetUid = targetUid,
                        RoomId = roomId,
                        Code = 0,
                        Content = "飘过~",
                        Completed = 0
                    };
                    await Globals.MessageRepository.Insert(message);
                }
            }

            await Globals.UserRepository.SetCompleted(0, uid);
        }
        else if (command == "/target_delete")
        {
            if (parameter == null) return;

            if (parameter == "all")
            {
                await Globals.TargetRepository.DeleteByUid(uid);
                return;
            }

            string targetUid = parameter;
            if (!targetUid.IsNumeric()) return;
            await Globals.TargetRepository.DeleteByUidAndTargetUid(uid, targetUid);
        }
        else if (command == "/message_set")
        {
            if (parameter == null) return;

            string[] pair = parameter.Split(" ", 2);
            if (pair.Length != 2) return;

            string targetUid = pair[0].Trim();
            string content = pair[1].Trim();
            int messageNum = await Globals.MessageRepository.GetMessageNum(uid);
            if (!targetUid.IsNumeric() || content.Length > 20 || messageNum > 30) return;

            bool exist = await Globals.MessageRepository.CheckExistByUidAndTargetUid(uid, targetUid);
            if (exist)
            {
                await Globals.MessageRepository.SetContentByUidAndTargetUid(content, uid, targetUid);
                await Globals.MessageRepository.SetCompletedByUidAndTargetUid(0, uid, targetUid);
                await Globals.MessageRepository.SetCodeAndResponseByUidAndTargetUid(0, null, uid, targetUid);
            }
            else
            {
                JToken? data = await GetRoomData(uid, targetUid);
                if (data == null) return;

                string? targetName = (string?)data["name"];
                string? roomId = (string?)data["live_room"]!["roomid"];

                var messageEntity = new MessageEntity
                {
                    Uid = uid,
                    TargetUid = targetUid,
                    TargetName = targetName,
                    RoomId = roomId,
                    Content = content,
                    Code = 0
                };

                await Globals.MessageRepository.Insert(messageEntity);
            }
        }
        else if (command == "/message_delete")
        {
            if (parameter == null) return;

            if (parameter == "all")
            {
                List<TargetEntity> targets = await Globals.TargetRepository.GetTargetsByUid(uid);
                foreach (var target in targets)
                {
                    bool exist = await Globals.TargetRepository.CheckExistByUidAndTargetUid(uid, target.TargetUid);
                    if (exist)
                    {
                        await Globals.MessageRepository.SetContentByUidAndTargetUid("飘过~", uid, target.TargetUid);
                        await Globals.MessageRepository.SetCodeAndResponseByUidAndTargetUid(0, null, uid,
                            target.TargetUid);
                    }
                    else
                    {
                        await Globals.MessageRepository.DeleteByUidAndTargetUid(uid, target.Uid);
                    }
                }

                return;
            }

            string targetUid = parameter;
            if (!targetUid.IsNumeric()) return;

            bool targetExist = await Globals.TargetRepository.CheckExistByUidAndTargetUid(uid, targetUid);
            if (targetExist)
            {
                await Globals.MessageRepository.SetContentByUidAndTargetUid("飘过~", uid, targetUid);
                await Globals.MessageRepository.SetCodeAndResponseByUidAndTargetUid(0, null, uid, targetUid);
            }
            else
            {
                await Globals.MessageRepository.DeleteByUidAndTargetUid(uid, targetUid);
            }
        }
        else if (command == "/cookie_commit")
        {
            try
            {
                if (parameter == null) return;

                var userEntity = await Globals.UserRepository.Get(uid);
                if (userEntity == null) return;

                userEntity.Cookie = parameter.Replace("\n", "");
                userEntity.Csrf = GetCsrf(userEntity.Cookie);
                userEntity.CookieStatus = 0;

                await Globals.UserRepository.Update(userEntity);
            }
#if DEBUG
            catch (Exception e)
            {
                Console.WriteLine(e);
                await _logger.Log($"uid {uid} 提交的cookie有误");
            }
#else
            catch 
            {
                await _logger.Log($"uid {uid} 提交的cookie有误");
            }
#endif
        }
        else if (command == "/config")
        {
            string? content = await _users[uid].GetConfigString(_logger);
            if (content == null) return;

            await SendMessage(content, uid);
        }
        else if (command == "/message_config")
        {
            string? content = await _users[uid].GetMessageConfigString(_logger);
            if (content == null) return;

            await SendMessage(content, uid);
        }
        else if (command == "/target_config")
        {
            string? content = await _users[uid].GetTargetConfigString(_logger);
            if (content == null) return;

            await SendMessage(content, uid);
        }
        else if (command == "/delete")
        {
            await Globals.TargetRepository.DeleteByUid(uid);
            await Globals.MessageRepository.DeleteByUid(uid);

            var userEntity = await Globals.UserRepository.Get(uid);
            if (userEntity == null) return;

            userEntity.Cookie = "";
            userEntity.Csrf = "";
            userEntity.Completed = 0;
            userEntity.CookieStatus = -1;

            await Globals.UserRepository.Update(userEntity);
            _users[uid] = userEntity;
        }
    }

    private async Task HandleMessages(string uid, int lastTimestamp, IEnumerable<JToken>? messages)
    {
        if (messages == null) return;
        foreach (var msg in messages)
        {
            //忽略 已读的、bot发送的、非文字的 消息
            if ((int?)msg["timestamp"] <= lastTimestamp || (string?)msg["sender_uid"] == _botEntity.Uid ||
                (int?)msg["msg_type"] != 1) continue;

            try
            {
                string? timestamp = (string?)msg["timestamp"];
                string? contentJson = (string?)msg["content"];
                if (timestamp == null || contentJson == null) return;

                _users[uid].ReadTimestamp = timestamp;
                string? content = (string?)JObject.Parse(contentJson)["content"];
                content = content?.Trim();
                await _logger.Log($"{uid}：{content}");
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
                        await HandleCommand(uid, command, parameter);
                    }
                    else
                    {
                        string command = pair[0].Trim();
                        await HandleCommand(uid, command, null);
                    }
                }
            }
            catch (JsonReaderException)
            {
            }
        }
    }

    private async Task HandleIncomingMessage()
    {
        JToken? sessionList = await _botEntity.GetSessionList(_logger);
        if (sessionList == null) return;
        _users = await GetUsers();
        foreach (var session in sessionList)
        {
            string? uid = (string?)session["talker_id"];
            if (uid == null) continue;

            int? timestamp = session["last_msg"]!.HasValues ? (int?)session["last_msg"]!["timestamp"] : 0;
            if (timestamp == null) continue;

            if (!_users.ContainsKey(uid)) //新用户
            {
                IEnumerable<JToken>? messages = await _botEntity.GetMessages(uid, _logger);
                var userEntity = new UserEntity
                {
                    Uid = uid,
                    Cookie = "",
                    Csrf = "",
                    ReadTimestamp = timestamp.ToString(),
                    ConfigTimestamp = "0",
                    ConfigNum = 0
                };
                _users.Add(uid, userEntity);
                await Globals.UserRepository.Insert(userEntity);
                await HandleMessages(uid, 0, messages);
            }
            else if (timestamp > Int32.Parse(_users[uid].ReadTimestamp!)) //发新消息的用户
            {
                int readTimestamp = Int32.Parse(_users[uid].ReadTimestamp!);
                IEnumerable<JToken>? messages = await _botEntity.GetMessages(uid, _logger);
                await Globals.UserRepository.SetReadTimestamp(timestamp.ToString()!, uid);
                await HandleMessages(uid, readTimestamp, messages);
            }
        }
    }

    private async Task BotMain()
    {
        while (true)
        {
            try
            {
                await CheckNewDay();
                await HandleIncomingMessage();
                Globals.ReceiveStatus = 0;
                if (_talking) Globals.SendStatus = 0;
            }
            catch (ApiException)
            {
                int cd = 15;
                Globals.SendStatus = -1;
                Globals.ReceiveStatus = -1;
                while (cd != 0)
                {
                    await _logger.Log($"请求过于频繁，还需冷却 {cd} 分钟");
                    await Task.Delay(cd * 60 * 1000);
                    cd--;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                await Task.Delay(2000);
            }
        }
    }

    public async Task Main()
    {
        var tasks = new List<Task>
        {
            _botEntity.UpdateSign(_logger), BotMain()
        };
        await Task.WhenAll(tasks);
    }
}