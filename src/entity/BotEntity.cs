using little_heart_bot_3.others;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace little_heart_bot_3.entity;

public class BotEntity
{
    public string? Uid { get; set; }
    public string? Cookie { get; set; }
    public string? Csrf { get; set; }
    public string? DevId { get; set; }
    public int AppStatus { get; set; }
    public int ReceiveStatus { get; set; }
    public int SendStatus { get; set; }

    private string MakeSign()
    {
        string sign = "给你【";
        if (Globals.AppStatus == 0)
        {
            sign += "弹幕、点赞、观看直播正常";
        }
        else if (Globals.AppStatus == -1)
        {
            sign += "弹幕、点赞、观看直播冷却中";
        }

        sign += "，";


        if (Globals.ReceiveStatus == 0)
        {
            sign += "接收私信正常";
        }
        else if (Globals.ReceiveStatus == -1)
        {
            sign += "接收私信冷却中";
        }

        sign += "，";

        if (Globals.SendStatus == 0)
        {
            sign += "发送私信正常";
        }
        else if (Globals.SendStatus == -1)
        {
            sign += "发送私信冷却中";
        }
        else if (Globals.SendStatus == -2)
        {
            sign += "发送私信已禁言";
        }

        sign += "】";
        return sign;
    }

    public async Task<JToken?> GetSessionList(Logger logger)
    {
        Again:
        try
        {
            //普通的私信session
            HttpResponseMessage responseMessage = await Globals.HttpClient.SendAsync(new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri =
                    new Uri("https://api.vc.bilibili.com/session_svr/v1/session_svr/get_sessions?session_type=1"),
                Headers = { { "Cookie", Cookie } },
            });
            await Task.Delay(1000);
            JObject response = JObject.Parse(await responseMessage.Content.ReadAsStringAsync());
            int? code = (int?)response["code"];

            if (code != 0)
            {
                await logger.Log(response);
                await logger.Log("获取普通的session_list失败");
                throw new ApiException();
            }

            JArray? sessionList = (JArray?)response["data"]!["session_list"];

            //被屏蔽的私信session
            responseMessage = await Globals.HttpClient.SendAsync(new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri =
                    new Uri("https://api.vc.bilibili.com/session_svr/v1/session_svr/get_sessions?session_type=5"),
                Headers = { { "Cookie", Cookie } },
            });
            response = JObject.Parse(await responseMessage.Content.ReadAsStringAsync());
            code = (int?)response["code"];

            if (code != 0)
            {
                await logger.Log(response);
                await logger.Log("获取被屏蔽的session_list失败");
                throw new ApiException();
            }

            JToken? blockedList = response["data"]!["session_list"];

            if (blockedList == null) return sessionList;

            foreach (var blockedSession in blockedList)
            {
                sessionList?.Add(blockedSession);
            }

            return sessionList;
        }
        catch (HttpRequestException)
        {
            await Task.Delay(1000);
            goto Again;
        }
    }

    public async Task<IEnumerable<JToken>?> GetMessages(string? uid, Logger logger)
    {
        var uri = new Uri(
            $"https://api.vc.bilibili.com/svr_sync/v1/svr_sync/fetch_session_msgs?talker_id={uid}&session_type=1");

        HttpResponseMessage responseMessage = await Globals.HttpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = uri,
            Headers = { { "Cookie", Cookie } },
        });
        await Task.Delay(1000);
        JObject response = JObject.Parse(await responseMessage.Content.ReadAsStringAsync());
        int? code = (int?)response["code"];

        if (code != 0)
        {
            await logger.Log(response);
            await logger.Log($"与 {uid} 的聊天记录获取失败");
            throw new ApiException();
        }

        return response["data"]!["messages"]?.Reverse();
    }

    public async Task UpdateSign(Logger logger)
    {
        while (true)
        {
            try
            {
                if (AppStatus != Globals.AppStatus || ReceiveStatus != Globals.ReceiveStatus ||
                    SendStatus != Globals.SendStatus)
                {
                    string sign = MakeSign();

                    var payload = new Dictionary<string, string?>
                    {
                        { "user_sign", sign },
                        { "jsonp", "jsonp" },
                        { "csrf", Csrf }
                    };

                    HttpResponseMessage responseMessage = await Globals.HttpClient.SendAsync(new HttpRequestMessage
                    {
                        Method = HttpMethod.Post,
                        RequestUri = new Uri("https://api.bilibili.com/x/member/web/sign/update"),
                        Headers = { { "Cookie", Cookie } },
                        Content = new FormUrlEncodedContent(payload)
                    });
                    await Task.Delay(1000);
                    JObject response = JObject.Parse(await responseMessage.Content.ReadAsStringAsync());
                    await logger.Log(response);
                    await logger.Log("签名改为：" + sign);
                    AppStatus = Globals.AppStatus;
                    ReceiveStatus = Globals.ReceiveStatus;
                    SendStatus = Globals.SendStatus;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {
                await Task.Delay(1000);
            }
        }
    }

    public async Task<bool> SendMessage(string content, string targetUid, Logger logger)
    {
        var payload = new Dictionary<string, string?>
        {
            { "msg[sender_uid]", Uid },
            { "msg[receiver_id]", targetUid },
            { "msg[receiver_type]", "1" },
            { "msg[msg_type]", "1" },
            { "msg[dev_id]", DevId },
            { "msg[timestamp]", DateTimeOffset.Now.ToUnixTimeSeconds().ToString() },
            { "msg[content]", new JObject { { "content", content } }.ToString(Formatting.None) },
            { "csrf", Csrf }
        };
        HttpResponseMessage responseMessage = await Globals.HttpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri("https://api.vc.bilibili.com/web_im/v1/web_im/send_msg"),
            Headers = { { "Cookie", Cookie } },
            Content = new FormUrlEncodedContent(payload)
        });
        await Task.Delay(1000);
        JObject response = JObject.Parse(await responseMessage.Content.ReadAsStringAsync());
        int? code = (int?)response["code"];

        if (code != 0)
        {
            await logger.Log(response);
            await logger.Log("私信发送失败");

            if (code == 21024) return true;

            return false;
        }

        return true;
    }
}