using System.Security.Cryptography;
using System.Text;
using little_heart_bot_3.entity;
using little_heart_bot_3.repository;
using MySqlConnector;
using Newtonsoft.Json.Linq;

namespace little_heart_bot_3.others;

public static class Globals
{
    public static int AppStatus { get; set; }
    public static int ReceiveStatus { get; set; }
    public static int SendStatus { get; set; }

    public static readonly HttpClient HttpClient;
    public static readonly BotRepository BotRepository;
    public static readonly MessageRepository MessageRepository;
    public static readonly TargetRepository TargetRepository;
    public static readonly UserRepository UserRepository;
    public static readonly string ConnectionString;

    static Globals()
    {
        string jsonString = File.ReadAllText("MysqlOption.json");
        JObject json = JObject.Parse(jsonString);
        var builder = new MySqlConnectionStringBuilder
        {
            Server = (string?)json["host"],
            Database = (string?)json["database"],
            UserID = (string?)json["user"],
            Password = (string?)json["password"]
        };
        ConnectionString = builder.ConnectionString;

        BotRepository = new BotRepository();
        MessageRepository = new MessageRepository();
        TargetRepository = new TargetRepository();
        UserRepository = new UserRepository();
        HttpClient = new HttpClient();
    }

    public static async Task<(string wRid, string wTs)> EncWbi(UserEntity userEntity,
        Dictionary<string, string> parameters, Logger? logger = null)
    {
        HttpResponseMessage responseMessage = await HttpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri("https://api.bilibili.com/x/web-interface/nav"),
        });

        JObject response = JObject.Parse(await responseMessage.Content.ReadAsStringAsync());
        int? code = (int?)response["code"];
        if (code != -101)
        {
            if (logger != null)
            {
                await logger.Log(response);
                await logger.Log($"uid {userEntity.Uid} 获取Wbi失败");
            }

            throw new ApiException();
        }

        string imgUrl = (string)response["data"]!["wbi_img"]!["img_url"]!;
        imgUrl = imgUrl.Split("/")[^1].Split(".")[0];

        string subUrl = (string)response["data"]!["wbi_img"]!["sub_url"]!;
        subUrl = subUrl.Split("/")[^1].Split(".")[0];

        string ae = imgUrl + subUrl;

        string me = GetMixinKey(ae);
        string wTs = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
        parameters["wts"] = wTs;
        var sortedParameters = parameters.OrderBy(p => p.Key).ToDictionary(p => p.Key, p => p.Value);
        ae = string.Join("&", sortedParameters.Select(p => $"{p.Key}={p.Value}"));
        byte[] aeWithMeBytes = Encoding.UTF8.GetBytes(ae + me);
        using MD5 md5 = MD5.Create();

        byte[] hashBytes = md5.ComputeHash(aeWithMeBytes);
        string wRid = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        parameters["w_rid"] = wRid;
        return (wRid, wTs);
    }

    private static string GetMixinKey(string ae)
    {
        int[] oe =
        {
            46, 47, 18, 2, 53, 8, 23, 32, 15, 50, 10, 31, 58, 3, 45, 35, 27, 43, 5, 49, 33, 9, 42, 19, 29, 28, 14, 39,
            12, 38, 41, 13, 37, 48, 7, 16, 24, 55, 40, 61, 26, 17, 0, 1, 60, 51, 30, 4, 22, 25, 54, 21, 56, 59, 6, 63,
            57, 62, 11, 36, 20, 34, 44, 52
        };
        string le = oe.Aggregate("", (s, i) => s + ae[i]);
        return le.Substring(0, 32);
    }
}