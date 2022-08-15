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

    public async Task SendMessage(Logger logger)
    {
        List<MessageEntity> messages = await Globals.MessageRepository.GetUncompletedMessagesByUid(Uid);

        foreach (var message in messages)
        {
            await message.Send(Cookie, Csrf, logger);
        }
    }

    public async Task WatchLive(Logger logger)
    {
        UserEntity? thisUser = await Globals.UserRepository.Get(Uid);
        if (thisUser == null || thisUser.CookieStatus != 1) return;

        List<TargetEntity> targets = await Globals.TargetRepository.GetUncompletedTargetsByUid(Uid);

#if DEBUG
        Console.WriteLine($"uid {Uid}: 未完成的目标数: {targets.Count}");
#endif
        if (targets.Count > 10)
        {
            targets = targets.GetRange(0, 10);
        }

        await logger.Log($"uid {Uid} 正在观看直播，目前同时观看 {targets.Count} 个目标");

        var tasks = new List<Task>();
        targets.ForEach(target => tasks.Add(target.Start(Cookie, Csrf, logger)));
        await Task.WhenAll(tasks);

        targets = await Globals.TargetRepository.GetUncompletedTargetsByUid(Uid);
        if (targets.Count != 0) return;

        Completed = 1;
        await Globals.UserRepository.SetCompleted(Completed, Uid);
    }

    public async Task<string?> GetConfigString(Logger logger)
    {
        long nowTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        if (ConfigNum >= 10 || nowTimestamp - Int64.Parse(ConfigTimestamp!) < 60) return null;

        List<TargetEntity> targets = await Globals.TargetRepository.GetTargetsByUid(Uid);

        string result = "";
        result += $"目标({targets.Count}/50)：\n";
        targets.ForEach(target => result += $"{target.TargetName}\n");

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

        if (result.Length > 450)
        {
            result = "设置的目标过多，配置信息长度大于500，超过了私信长度的上限，无法发送\n";
        }

        string targetMsg = Completed == 1 ? "是" : "否";
        result += $"今日任务是否已完成：{targetMsg}\n";
        result += $"已用查询次数({ConfigNum + 1}/10)\n";
        return result;
    }

    public async Task<string?> GetMessageConfigString(Logger logger)
    {
        long nowTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        if (ConfigNum >= 10 || nowTimestamp - Int64.Parse(ConfigTimestamp!) < 60) return null;

        List<MessageEntity> messages = await Globals.MessageRepository.GetMessagesByUid(Uid);
        string result = "";
        result += $"弹幕({messages.Count}/50)：\n\n";
        messages.ForEach(message =>
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

        if (result.Length > 450)
        {
            result = "设置的弹幕过多，配置信息长度大于500，超过了私信长度的上限，无法发送\n";
        }

        result += $"已用查询次数({ConfigNum + 1}/10)\n";
        return result;
    }

    public async Task<string?> GetTargetConfigString(Logger logger)
    {
        long nowTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        if (ConfigNum >= 10 || nowTimestamp - Int64.Parse(ConfigTimestamp!) < 60) return null;

        List<TargetEntity> targets = await Globals.TargetRepository.GetTargetsByUid(Uid);
        string result = "";
        result += $"目标({targets.Count}/50)\n\n";
        result += "观看时长(分钟)：\n";
        targets.ForEach(target => { result += $"{target.TargetName}：{target.WatchedSeconds / 60}\n"; });
        result += "\n";

        if (result.Length > 450)
        {
            result = "设置的目标过多，配置信息长度大于500，超过了私信长度的上限，无法发送\n";
        }

        result += $"已用查询次数({ConfigNum + 1}/10)\n";
        return result;
    }
}