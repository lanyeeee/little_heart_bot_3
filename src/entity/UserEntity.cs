using System.Text;
using little_heart_bot_3.others;
using Newtonsoft.Json.Linq;

namespace little_heart_bot_3.entity;

public class UserEntity
{
    public string? Uid { get; set; }
    public string? Cookie { get; set; }
    public string? Csrf { get; set; }
    public int Completed { get; set; }
    public int CookieStatus { get; set; }
    public int ConfigNum { get; set; }
    public int TargetNum { get; set; }
    public string? ReadTimestamp { get; set; }
    public string? ConfigTimestamp { get; set; }

    //一对多
    public List<MessageEntity>? Messages { get; set; }

    //一对多
    public List<TargetEntity>? Targets { get; set; }

    public async Task SendMessage(Logger logger)
    {
        if (Messages == null) return;
        foreach (var message in Messages)
        {
            await message.Send(Cookie, Csrf, logger);
        }
    }

    public async Task WatchLive(Logger logger)
    {
        UserEntity? thisUser = await Globals.UserRepository.Get(Uid);
        if (thisUser == null || thisUser.CookieStatus != 1) return;
        
        if(Targets==null) return;

        int maxCountPerRound = 10;//每个用户每轮最多同时观看多少个直播
        int selectedCount = 0;//已经在观看的直播数
        var tasks = new List<Task>();
        
        foreach (var target in Targets)
        {
            if(target.Completed == 1) continue;//已完成的任务就跳过
            
            tasks.Add(target.Start(Cookie, Csrf, logger));
            
            selectedCount++;
            if(selectedCount >= maxCountPerRound) break;
        }
        
        await logger.Log($"uid {Uid} 正在观看直播，目前同时观看 {selectedCount} 个目标");
        await Task.WhenAll(tasks);

        //如果有任何一个任务未完成
        if(Targets.Any(t=>t.Completed!=1)) return;
        
        //如果所有任务都完成了
        Completed = 1;
        await Globals.UserRepository.SetCompleted(Completed, Uid);
    }

    public string? GetConfigString(Logger logger)
    {
        long nowTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        if (ConfigNum >= 10 || nowTimestamp - Int64.Parse(ConfigTimestamp!) < 60) return null;

        if (Targets == null) return null;

        string result = "";
        result += $"目标({Targets.Count}/50)：\n";
        Targets.ForEach(target => result += $"{target.TargetName}\n");

        if (result.Length > 350)
        {
            result = $"目标({Targets.Count}/50)：\n目标过多，信息超过了私信长度的上限，所以/config里无法携带目标的配置信息，请尝试使用/target_config查看目标配置\n";
        }

        result += "\n";
        if (string.IsNullOrEmpty(Cookie))
        {
            result += "cookie：无\n";
        }
        else
        {
            string cookieMsg = "";
            if (CookieStatus == -1) cookieMsg = "错误或已过期";
            else if (CookieStatus == 0) cookieMsg = "还未被使用";
            else if (CookieStatus == 1) cookieMsg = "直到上次使用还有效";
            result += $"cookie：有，{cookieMsg}\n";
        }


        string targetMsg = Completed == 1 ? "是" : "否";
        result += $"今日任务是否已完成：{targetMsg}\n";
        result += $"已用查询次数({ConfigNum + 1}/10)\n";
        return result;
    }

    public string? GetMessageConfigString(Logger logger)
    {
        long nowTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        if (ConfigNum >= 10 || nowTimestamp - Int64.Parse(ConfigTimestamp!) < 60) return null;

        if (Messages == null) return null;

        string result = "";
        result += $"弹幕({Messages.Count}/50)：\n\n";
        Messages.ForEach(message =>
        {
            result += $"{message.TargetName}：{message.Content}\n";
            string statusMsg;
            if (message.Response == null)
            {
                statusMsg = "未发送\n";
            }
            else
            {
                JObject response = JObject.Parse(message.Response);
                statusMsg = $"已发送，代码:{message.Code}，信息:{(string?)response["message"]}\n";
            }

            result += $"状态：{statusMsg}\n";
        });


        return result;
    }

    public List<string>? GetMessageConfigStringSplit(Logger logger)
    {
        string? content = GetMessageConfigString(logger);

        if (content == null)
        {
            return null;
        }

        return SplitString(content, 400);
    }

    public async Task<string?> GetMessageConfigString(string targetUid, Logger logger)
    {
        long nowTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        if (ConfigNum >= 10 || nowTimestamp - Int64.Parse(ConfigTimestamp!) < 60) return null;

        MessageEntity? message = await Globals.MessageRepository.GetMessagesByUidAndTargetUid(Uid, targetUid);
        if (message == null) return null;

        string result = $"{message.TargetName}：{message.Content}\n";
        string statusMsg;
        if (message.Response == null)
        {
            statusMsg = "未发送\n";
        }
        else
        {
            JObject response = JObject.Parse(message.Response);
            statusMsg = $"已发送，代码:{message.Code}，信息:{(string?)response["message"]}\n";
        }

        result += $"状态：{statusMsg}\n";
        return result;
    }

    public string? GetTargetConfigString(Logger logger)
    {
        long nowTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        if (ConfigNum >= 10 || nowTimestamp - Int64.Parse(ConfigTimestamp!) < 60) return null;

        if (Targets == null) return null;

        string result = "";
        result += $"目标({Targets.Count}/50)\n\n";
        result += "观看时长(分钟)：\n";
        Targets.ForEach(target => { result += $"{target.TargetName}：{target.WatchedSeconds / 60}\n"; });
        result += "\n";

        return result;
    }

    public List<string>? GetTargetConfigStringSplit(Logger logger)
    {
        string? content = GetTargetConfigString(logger);

        if (content == null)
        {
            return null;
        }

        return SplitString(content, 400);
    }

    public async Task<string?> GetTargetConfigString(string targetUid, Logger logger)
    {
        long nowTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        if (ConfigNum >= 10 || nowTimestamp - Int64.Parse(ConfigTimestamp!) < 60) return null;

        TargetEntity? target = await Globals.TargetRepository.GetTargetsByUidAndTargetUid(Uid, targetUid);
        if (target == null) return null;

        string result = "观看时长(分钟)：\n";
        result += $"{target.TargetName}：{target.WatchedSeconds / 60}\n";
        result += "\n";

        return result;
    }

    private List<string> SplitString(string config, int maxLength)
    {
        // 将字符串按照换行符分割成行
        string[] lines = config.Split("\n");

        // 将行重新拼接成长度不超过maxLength的小段
        List<string> contents = new List<string>();
        StringBuilder contentBuilder = new StringBuilder();
        foreach (string line in lines)
        {
            if (contentBuilder.Length + line.Length > maxLength)
            {
                contents.Add(contentBuilder.ToString());
                contentBuilder.Clear();
            }

            contentBuilder.Append(line);
            contentBuilder.Append('\n');
        }

        contents.Add(contentBuilder.ToString());
        return contents;
    }
}