// See https://aka.ms/new-console-template for more information

using Dapper;

namespace little_heart_bot_3.main;

public static class Program
{
    public static async Task Main(string[] args)
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;

#if DEBUG
        await Test();
#endif
        var tasks = new List<Task>
        {
            App.Instance.Main(),
            Bot.Instance.Main()
        };
        await Task.WhenAll(tasks);
    }

    private static async Task Test()
    {
    }
}