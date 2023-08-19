using System.Security.Cryptography;
using System.Text;
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

    public static Dictionary<string, string> EncWbi(Dictionary<string, string> parameters, string imgKey, string subKey)
    {
        string mixinKey = GetMixinKey(imgKey + subKey);
        string currTime = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
        //添加 wts 字段
        parameters["wts"] = currTime;
        // 按照 key 重排参数
        parameters = parameters.OrderBy(p => p.Key).ToDictionary(p => p.Key, p => p.Value);
        //过滤 value 中的 "!'()*" 字符
        parameters = parameters.ToDictionary(
            kvp => kvp.Key,
            kvp => new string(kvp.Value.Where(chr => !"!'()*".Contains(chr)).ToArray())
        );
        // 序列化参数
        string query = new FormUrlEncodedContent(parameters).ReadAsStringAsync().Result;
        //计算 w_rid
        using MD5 md5 = MD5.Create();
        byte[] hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(query + mixinKey));
        string wbiSign = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        parameters["w_rid"] = wbiSign;

        return parameters;
    }


    private static readonly int[] MixinKeyEncTab =
    {
        46, 47, 18, 2, 53, 8, 23, 32, 15, 50, 10, 31, 58, 3, 45, 35, 27, 43, 5, 49, 33, 9, 42, 19, 29, 28, 14, 39,
        12, 38, 41, 13, 37, 48, 7, 16, 24, 55, 40, 61, 26, 17, 0, 1, 60, 51, 30, 4, 22, 25, 54, 21, 56, 59, 6, 63,
        57, 62, 11, 36, 20, 34, 44, 52
    };

    private static string GetMixinKey(string orig)
    {
        return MixinKeyEncTab.Aggregate("", (s, i) => s + orig[i])[..32];
    }

    public static async Task<(string, string)> GetWbiKeys()
    {
        HttpResponseMessage responseMessage = await HttpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri("https://api.bilibili.com/x/web-interface/nav"),
        });

        JObject response = JObject.Parse(await responseMessage.Content.ReadAsStringAsync())!;

        string imgUrl = (string)response["data"]!["wbi_img"]!["img_url"]!;
        imgUrl = imgUrl.Split("/")[^1].Split(".")[0];

        string subUrl = (string)response["data"]!["wbi_img"]!["sub_url"]!;
        subUrl = subUrl.Split("/")[^1].Split(".")[0];
        return (imgUrl, subUrl);
    }
}