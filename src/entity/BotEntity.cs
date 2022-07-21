using little_heart_bot_3.others;
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
            sign += "弹幕、点赞、分享正常";
        }
        else if (Globals.AppStatus == -1)
        {
            sign += "弹幕、点赞、分享正常冷却中";
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
}