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

    public async Task Send(string? cookie, string? csrf, Logger logger)
    {
        if (Code != 0) return;

        JObject response = await PostMessage(cookie, csrf);

        Code = (int?)response["code"];
        Response = response.ToString();

        if (Code == 0)
        {
            Completed = 1;
            await Globals.MessageRepository.Save(this);
        }
        else if (Code == -412)
        {
            await logger.Log(response);
            throw new ApiException();
        }
        else
        {
            await logger.Log(response);
            await logger.Log($"uid {Uid} 弹幕发送失败");
            await Globals.MessageRepository.Save(this);
        }

        await Task.Delay(3000);
    }
}