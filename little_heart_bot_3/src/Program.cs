using System.Text.Encodings.Web;
using System.Text.Json;
using little_heart_bot_3.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Enrichers.WithCaller;
using Serilog.Templates;

namespace little_heart_bot_3;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddSingleton<HttpClient>();
        builder.Services.AddSingleton<JsonSerializerOptions>(_ => new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        var logFormatter = new ExpressionTemplate("{ {ts:@t, template:@mt, msg:@m, level:@l, ex:@x, p:{..@p}} }\n");
        builder.Services.AddKeyedSingleton<ILogger>("bot:Logger", (_, _) => new LoggerConfiguration()
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

        builder.Services.AddKeyedSingleton<ILogger>("app:Logger", (_, _) => new LoggerConfiguration()
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

//BotService
        builder.Services.AddKeyedScoped<IBotService, Services.Implements.Bot.BotService>("bot:BotService");
        builder.Services.AddKeyedScoped<IBotService, Services.Implements.App.BotService>("app:BotService");
//MessageService
        builder.Services.AddKeyedScoped<IMessageService, Services.Implements.Bot.MessageService>("bot:MessageService");
        builder.Services.AddKeyedScoped<IMessageService, Services.Implements.App.MessageService>("app:MessageService");
//TargetService
        builder.Services.AddKeyedScoped<ITargetService, Services.Implements.Bot.TargetService>("bot:TargetService");
        builder.Services.AddKeyedScoped<ITargetService, Services.Implements.App.TargetService>("app:TargetService");
//UserService
        builder.Services.AddKeyedScoped<IUserService, Services.Implements.Bot.UserService>("bot:UserService");
        builder.Services.AddKeyedScoped<IUserService, Services.Implements.App.UserService>("app:UserService");

        builder.Services.AddHostedService<Bot>();
        builder.Services.AddHostedService<App>();

        var host = builder.Build();
        host.Run();
    }
}


// public static class Program
// {
//     public static async Task Main(string[] args)
//     {
//         var provider = ConfigService();
//         App app = provider.GetRequiredService<App>();
//         Bot bot = provider.GetRequiredService<Bot>();
//         Console.WriteLine("Running...");
//         await Task.WhenAll(app.Main(), bot.Main());
//     }
//
//     public static ServiceProvider ConfigService()
//     {
//         var services = new ServiceCollection();
//
//         services.AddSingleton<HttpClient>();
//         services.AddSingleton<JsonSerializerOptions>(_ => new JsonSerializerOptions
//         {
//             Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
//         });
//
//         var logFormatter = new ExpressionTemplate("{ {ts:@t, template:@mt, msg:@m, level:@l, ex:@x, p:{..@p}} }\n");
//         services.AddKeyedSingleton<ILogger>("bot:Logger", (_, _) => new LoggerConfiguration()
//             .MinimumLevel.Debug()
//             .WriteTo.File(
//                 path: "logs/bot/bot-.txt",
//                 rollingInterval: RollingInterval.Day,
//                 fileSizeLimitBytes: 1 * 1024 * 1024,
//                 rollOnFileSizeLimit: true,
//                 buffered: true,
//                 flushToDiskInterval: TimeSpan.FromSeconds(10),
//                 formatter: logFormatter)
//             .Enrich.WithCaller(true)
//             .CreateLogger());
//
//         services.AddKeyedSingleton<ILogger>("app:Logger", (_, _) => new LoggerConfiguration()
//             .MinimumLevel.Debug()
//             .WriteTo.File(
//                 path: "logs/app/app-.txt",
//                 rollingInterval: RollingInterval.Day,
//                 fileSizeLimitBytes: 2 * 1024 * 1024,
//                 rollOnFileSizeLimit: true,
//                 buffered: true,
//                 flushToDiskInterval: TimeSpan.FromSeconds(1),
//                 formatter: logFormatter)
//             .Enrich.WithCaller(true)
//             .CreateLogger());
//
//         //BotService
//         services.AddKeyedScoped<IBotService, Services.Implements.Bot.BotService>("bot:BotService");
//         services.AddKeyedScoped<IBotService, Services.Implements.App.BotService>("app:BotService");
//         //MessageService
//         services.AddKeyedScoped<IMessageService, Services.Implements.Bot.MessageService>("bot:MessageService");
//         services.AddKeyedScoped<IMessageService, Services.Implements.App.MessageService>("app:MessageService");
//         //TargetService
//         services.AddKeyedScoped<ITargetService, Services.Implements.Bot.TargetService>("bot:TargetService");
//         services.AddKeyedScoped<ITargetService, Services.Implements.App.TargetService>("app:TargetService");
//         //UserService
//         services.AddKeyedScoped<IUserService, Services.Implements.Bot.UserService>("bot:UserService");
//         services.AddKeyedScoped<IUserService, Services.Implements.App.UserService>("app:UserService");
//
//         services.AddSingleton<Bot>();
//         services.AddSingleton<App>();
//
//         services.AddSingleton<IServiceProvider>(provider => provider);
//         return services.BuildServiceProvider();
//     }
// }