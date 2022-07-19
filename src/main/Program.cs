// See https://aka.ms/new-console-template for more information

using System.Net.Http.Json;
using System.Text;
using Dapper;
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
        var payload = new JObject()
        {
            { "111", new JArray { 1, 2, 3 } }
        };
        Console.WriteLine(payload.ToString(Formatting.None));

        // HttpResponseMessage responseMessage = await Globals.HttpClient.SendAsync(new HttpRequestMessage
        // {
        //     Method = HttpMethod.Post,
        //     RequestUri = new Uri("http://localhost:5005"),
        //     Headers = { { "Cookie", "111" } },
        //     Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
        // });
        // Console.WriteLine(await responseMessage.Content.ReadAsStringAsync());
    }
}