using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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
        var uri = new Uri($"https://api.live.bilibili.com/xlive/web-room/v1/index/getInfoByRoom?&room_id={RoomId}");

        HttpResponseMessage responseMessage = await Globals.HttpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = uri
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

    private async Task<string?> GenerateS(Dictionary<string, string?> payload, string ts)
    {
        var t = new JObject
        {
            { "id", JArray.Parse(payload["id"]!) },
            { "device", payload["device"] },
            { "ets", int.Parse(payload["ets"]!) },
            { "benchmark", payload["secret_key"] },
            { "time", int.Parse(payload["heartbeat_interval"]!) },
            { "ts", long.Parse(ts) },
            { "ua", payload["ua"] }
        };

        var sPayload = new JObject
        {
            { "t", t },
            { "r", payload["secret_rule"] }
        };

        HttpResponseMessage responseMessage = await Globals.HttpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri("http://localhost:3000/enc"),
            Content = new StringContent(JsonConvert.SerializeObject(sPayload), Encoding.UTF8, "application/json")
        });
        JObject response = JObject.Parse(await responseMessage.Content.ReadAsStringAsync());

        return (string?)response["s"];
    }

    private async Task PostX(string? cookie, Dictionary<string, string?> payload, Logger logger)
    {
        string ts = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
        var xPayload = new Dictionary<string, string?>
        {
            { "s", await GenerateS(payload, ts) },
            { "id", payload["id"] },
            { "device", payload["device"] },
            { "ets", payload["ets"] },
            { "benchmark", payload["secret_key"] },
            { "time", payload["heartbeat_interval"] },
            { "ts", ts },
            { "ua", payload["ua"] },
            { "csrf_token", payload["csrf"] },
            { "csrf", payload["csrf"] },
            { "visit_id", "" }
        };
        HttpResponseMessage responseMessage = await Globals.HttpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri("https://live-trace.bilibili.com/xlive/data-interface/v1/x25Kn/X"),
            Headers = { { "Cookie", cookie } },
            Content = new FormUrlEncodedContent(xPayload)
        });
        JObject response = JObject.Parse(await responseMessage.Content.ReadAsStringAsync());

        int? code = (int?)response["code"];
        if (code != 0)
        {
            await logger.Log(response);
            await logger.Log($"uid {Uid} 发送X心跳包失败");
            throw new ApiException();
        }

        payload["ets"] = (string?)response["data"]!["timestamp"];
        payload["secret_key"] = (string?)response["data"]!["secret_key"];
        payload["heartbeat_interval"] = (string?)response["data"]!["heartbeat_interval"];
        JArray id = JArray.Parse(payload["id"]!);
        id[2] = (int?)id[2] + 1;
        payload["id"] = id.ToString(Formatting.None);
    }

    private async Task<int> GetExp()
    {
        var uri = new Uri(
            $"https://api.live.bilibili.com/fans_medal/v1/fans_medal/get_fans_medal_info?uid={Uid}&target_id={TargetUid}");

        HttpResponseMessage responseMessage = await Globals.HttpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = uri
        });

        JObject response = JObject.Parse(await responseMessage.Content.ReadAsStringAsync());
        return (int)response["data"]!["today_feed"]!;
    }

    private async Task<bool> IsCompleted()
    {
        Exp = await GetExp();
        await Globals.TargetRepository.SetExp(Exp, Id);
#if DEBUG
        Console.WriteLine($"uid {Uid}: {TargetName}:{Exp}");
#endif

        if (Exp != 1500) return false;

        Completed = 1;
        await Globals.TargetRepository.SetCompleted(Completed, Id);
        return true;
    }

    private async Task HeartBeat(string? cookie, Dictionary<string, string?> payload, Logger logger)
    {
        int interval = int.Parse(payload["heartbeat_interval"]!);
        int watchSeconds = 0;

        await Task.Delay(interval * 1000);

        while (true)
        {
            await PostX(cookie, payload, logger);

            interval = int.Parse(payload["heartbeat_interval"]!);
            watchSeconds += interval;

            //观看时长超过70分钟则返回
            if (watchSeconds >= 70 * 60) return;

            //每隔5分钟检查一次是否完成，完成则返回
            if (watchSeconds % 300 == 0 && await IsCompleted()) return;

            await Task.Delay(interval * 1000);
        }
    }

    public async Task Start(string? cookie, string? csrf, Logger logger)
    {
        if (await IsCompleted()) return;

        Dictionary<string, string?> payload = await PostE(cookie, csrf, logger);

        JArray id = JArray.Parse(payload["id"]!);
        if ((int?)id[0] == 0 || (int?)id[1] == 0) return;

        await HeartBeat(cookie, payload, logger);
    }
}