using little_heart_bot_3.others;
using Newtonsoft.Json.Linq;

namespace little_heart_bot_3.entity;

public class MessageEntity
{
    public int Id { get; set; }
    public string? Uid { get; set; }
    public string? TargetUid { get; set; }
    public string? TargetName { get; set; }
    public string? RoomId { get; set; }
    public string? Content { get; set; }
    public int? Code { get; set; }
    public string? Response { get; set; }
    public int Completed { get; set; }

    private async Task<JObject> PostMessage(string? cookie, string? csrf)
    {
        var payload = new Dictionary<string, string?>
        {
            { "bubble", "0" },
            { "msg", Content },
            { "color", "16777215" },
            { "mode", "1" },
            { "fontsize", "25" },
            { "rnd", DateTimeOffset.Now.ToUnixTimeSeconds().ToString() },
            { "roomid", RoomId },
            { "csrf", csrf },
            { "csrf_token", csrf }
        };

        HttpResponseMessage response = await Globals.HttpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri("https://api.live.bilibili.com/msg/send"),
            Headers = { { "Cookie", cookie } },
            Content = new FormUrlEncodedContent(payload)
        });

        return JObject.Parse(await response.Content.ReadAsStringAsync());
    }

    private async Task ThumbsUp(string? cookie, string? csrf, Logger logger)
    {
        var payload = new Dictionary<string, string?>
        {
            { "roomid", RoomId },
            { "csrf", csrf },
            { "csrf_token", csrf }
        };
        HttpResponseMessage responseMessage = await Globals.HttpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri("https://api.live.bilibili.com/xlive/web-ucenter/v1/interact/likeInteract"),
            Headers = { { "Cookie", cookie } },
            Content = new FormUrlEncodedContent(payload)
        });
        JObject response = JObject.Parse(await responseMessage.Content.ReadAsStringAsync());
        int? code = (int?)response["code"];

        if (code == -111 || code == -101)
        {
            await logger.Log(response);
            await logger.Log($"uid {Uid} 给 {TargetName} 点赞失败");
            await Globals.UserRepository.MarkCookieError(Uid);
            await Globals.MessageRepository.MarkCookieError(Code, Response, Uid);
        }
        else if (code != 0)
        {
            await logger.Log(response);
            await logger.Log($"uid {Uid} 给 {TargetName} 点赞失败");
        }
#if DEBUG
        if (code == 0) Console.WriteLine($"uid {Uid} 给 {TargetName} 点赞成功");
#endif
    }

    public async Task Send(string? cookie, string? csrf, Logger logger)
    {
        if (Completed == 1) return;

        await ThumbsUp(cookie, csrf, logger);

        if (Code != 0) return;

        JObject response = await PostMessage(cookie, csrf);

        Code = (int?)response["code"];
        Response = response.ToString();
        await Globals.MessageRepository.SetCodeAndResponse(Code, Response, Id);

        if (Code == 0)
        {
            Completed = 1;
            await Globals.MessageRepository.SetCompleted(Completed, Id);
#if DEBUG
            Console.WriteLine($"uid {Uid} 给 {TargetName} 发送弹幕成功");
#endif
        }
        else if (Code == -412)
        {
            await logger.Log(response);
            await logger.Log($"uid {Uid} 给 {TargetName} 发送弹幕失败");
            throw new ApiException();
        }
        else if (Code == -111 || Code == -101)
        {
            await logger.Log(response);
            await logger.Log($"uid {Uid} 给 {TargetName} 发送弹幕失败");
            await Globals.UserRepository.MarkCookieError(Uid);
            await Globals.MessageRepository.MarkCookieError(Code, Response, Uid);
        }
        else
        {
            await logger.Log(response);
            await logger.Log($"uid {Uid} 给 {TargetName} 发送弹幕失败");
        }

        await Task.Delay(3000);
    }
}