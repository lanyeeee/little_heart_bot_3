using System.Text.Json.Nodes;
using Dapper;
using little_heart_bot_3.Others;
using little_heart_bot_3.Repositories;
using little_heart_bot_3.Repositories.Implements;
using little_heart_bot_3.Services;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Serilog;
using Serilog.Core;
using Serilog.Enrichers.WithCaller;
using Serilog.Templates;


namespace little_heart_bot_3;

public static class Program
{
    public static async Task Main(string[] args)
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;

#if DEBUG
        await Test();
#endif
        App app = Globals.ServiceProvider.GetRequiredService<App>();
        Bot bot = Globals.ServiceProvider.GetRequiredService<Bot>();
        Console.WriteLine("Running...");
        await Task.WhenAll(app.Main(), bot.Main());
    }

    private static async Task Test()
    {
        await Task.Delay(1);
    }

    public static ServiceProvider ConfigService()
    {
        var services = new ServiceCollection();

        var logFormatter = new ExpressionTemplate("{ {ts:@t, template:@mt, msg:@m, level:@l, ex:@x, p:{..@p}} }\n");
        services.AddKeyedSingleton<Logger>("bot:Logger", (_, _) => new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                path: "logs/bot/bot-.txt",
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: 1 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                buffered: true,
                flushToDiskInterval: TimeSpan.FromSeconds(10),
                formatter: logFormatter)
            .Enrich.WithCaller(true)
            .CreateLogger());

        services.AddKeyedSingleton<Logger>("app:Logger", (_, _) => new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                path: "logs/app/app-.txt",
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: 2 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                buffered: true,
                flushToDiskInterval: TimeSpan.FromSeconds(1),
                formatter: logFormatter)
            .Enrich.WithCaller(true)
            .CreateLogger());

        services.AddTransient<IBotRepository, BotRepository>();
        services.AddTransient<IMessageRepository, MessageRepository>();
        services.AddTransient<ITargetRepository, TargetRepository>();
        services.AddTransient<IUserRepository, UserRepository>();

        //BotService
        services.AddKeyedTransient<IBotService, Services.Implements.Bot.BotService>("bot:BotService");
        services.AddKeyedTransient<IBotService, Services.Implements.App.BotService>("app:BotService");
        //MessageService
        services.AddKeyedTransient<IMessageService, Services.Implements.Bot.MessageService>("bot:MessageService");
        services.AddKeyedTransient<IMessageService, Services.Implements.App.MessageService>("app:MessageService");
        //TargetService
        services.AddKeyedTransient<ITargetService, Services.Implements.Bot.TargetService>("bot:TargetService");
        services.AddKeyedTransient<ITargetService, Services.Implements.App.TargetService>("app:TargetService");
        //UserService
        services.AddKeyedTransient<IUserService, Services.Implements.Bot.UserService>("bot:UserService");
        services.AddKeyedTransient<IUserService, Services.Implements.App.UserService>("app:UserService");

        services.AddSingleton<Bot>();
        services.AddSingleton<App>();

        return services.BuildServiceProvider();
    }

    public static string GetMysqlConnectionString()
    {
        var jsonString = File.ReadAllText("MysqlOption.json");
        JsonNode json = JsonNode.Parse(jsonString)!;
        var builder = new MySqlConnectionStringBuilder
        {
            Server = (string?)json["host"],
            Database = (string?)json["database"],
            UserID = (string?)json["user"],
            Password = (string?)json["password"]
        };
        return builder.ConnectionString;
    }
}