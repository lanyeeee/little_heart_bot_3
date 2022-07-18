// See https://aka.ms/new-console-template for more information

using Dapper;
using MySqlConnector;
using little_heart_bot_3.entity;
using little_heart_bot_3.others;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace little_heart_bot_3.main;

public static class Program
{
    public static async Task Main(string[] args)
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        // await Test();
        var tasks = new List<Task>
        {
            App.Instance.Main(),
            Bot.Instance.Main()
        };
        await Task.WhenAll(tasks);
    }

    private static async Task Test()
    {
        var payload = new Dictionary<string, string?>
        {
            { "id", "[9, 371, 0, 22634198]" },
            { "device", "[\"AUTO3216548823847252\", \"9771c6cb-1177-4263-97cd-d4bdd13b16b3\"]" },
            { "ts", "1658143688948" },
            { "is_patch", "0" },
            { "heart_beat", "[]" },
            {
                "ua",
                "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/102.0.0.0 Safari/537.36"
            },
            { "csrf_token", "3e9cd19d5b382b70eaa75fdfd6adfa6c" },
            { "csrf", "3e9cd19d5b382b70eaa75fdfd6adfa6c" },
            { "visit_id", "" }
        };

        foreach (var keyValuePair in payload)
        {
            Console.WriteLine($"{keyValuePair.Key} : {keyValuePair.Value}");
        }

        HttpResponseMessage responseMessage = await Globals.HttpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri("https://live-trace.bilibili.com/xlive/data-interface/v1/x25Kn/E"),
            Headers =
            {
                { "Cookie", "SESSDATA=f3debe02%2C1673317395%2C7f301%2A71; bili_jct=3e9cd19d5b382b70eaa75fdfd6adfa6c;" }
            },
            Content = new FormUrlEncodedContent(payload)
        });
        JObject response = JObject.Parse(await responseMessage.Content.ReadAsStringAsync());
    }
}