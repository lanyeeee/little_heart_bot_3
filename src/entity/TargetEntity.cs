using System.Net.Http.Headers;
using little_heart_bot_3.others;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace little_heart_bot_3.entity;

public class TargetEntity
{
    public int Id { get; set; }
    public string? Uid { get; set; }
    public string? TargetUid { get; set; }
    public string? TargetName { get; set; }
    public string? RoomId { get; set; }
    public int Exp { get; set; }
    public int Completed { get; set; }

    private async Task<Dictionary<string, string?>> GetPayload(string? cookie, string? csrf, Logger logger)
    {
        HttpResponseMessage responseMessage = await Globals.HttpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri =
                new Uri($"https://api.live.bilibili.com/xlive/web-room/v1/index/getInfoByRoom?&room_id={RoomId}"),
            Headers = { { "Cookie", cookie } }
        });
        JObject response = JObject.Parse(await responseMessage.Content.ReadAsStringAsync());

        int? code = (int?)response["code"];
        if (code != 0)
        {
            await logger.Log(response);
            await logger.Log($"uid {Uid} 获取直播间信息失败");
            throw new ApiException();
        }

        int? parentAreaId = (int?)response["data"]!["room_info"]!["parent_area_id"];
        int? areaId = (int?)response["data"]!["room_info"]!["area_id"];
        var id = new JArray { parentAreaId, areaId, 0, int.Parse(RoomId!) };

        return new Dictionary<string, string?>
        {
            { "id", id.ToString(Formatting.None) },
            { "device", "[\"AUTO8716422349901853\",\"3E739D10D-174A-10DD5-61028-A5E3625BE56450692infoc\"]" },
            { "ts", DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString() },
            { "is_patch", "0" },
            { "heart_beat", "[]" },
            {
                "ua",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/80.0.3987.163 Safari/537.36"
            },
            { "csrf_token", csrf },
            { "csrf", csrf },
            { "visit_id", "" }
        };
    }

    private async Task<Dictionary<string, string?>> PostE(string? cookie, string? csrf, Logger logger)
    {
        Dictionary<string, string?> payload = await GetPayload(cookie, csrf, logger);

        HttpResponseMessage responseMessage = await Globals.HttpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri("https://live-trace.bilibili.com/xlive/data-interface/v1/x25Kn/E"),
            Headers = { { "Cookie", cookie } },
            Content = new FormUrlEncodedContent(payload)
        });

        JObject response = JObject.Parse(await responseMessage.Content.ReadAsStringAsync());

        Console.WriteLine(response);
        int? code = (int?)response["code"];
        if (code != 0)
        {
            await logger.Log(response);
            await logger.Log($"uid {Uid} 发送E心跳包失败");
            throw new ApiException();
        }

        payload["ets"] = (string?)response["data"]!["timestamp"];
        payload["secret_key"] = (string?)response["data"]!["secret_key"];
        payload["heartbeat_interval"] = (string?)response["data"]!["heartbeat_interval"];
        payload["secret_rule"] = response["data"]!["secret_rule"]!.ToString(Formatting.None);

        return payload;
    }


    public async Task Start(string? cookie, string? csrf, Logger logger)
    {
        Dictionary<string, string?> payload = await PostE(cookie, csrf, logger);
    }
}