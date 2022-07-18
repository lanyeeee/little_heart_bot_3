// See https://aka.ms/new-console-template for more information

using Dapper;
using MySqlConnector;
using little_heart_bot_3.entity;
using little_heart_bot_3.others;
using Newtonsoft.Json.Linq;

namespace little_heart_bot_3.main;

public static class Program
{
    public static async Task Main(string[] args)
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        var tasks = new List<Task>
        {
            App.Instance.Main(),
            Bot.Instance.Main()
        };
        await Task.WhenAll(tasks);
    }
}