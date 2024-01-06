using System.Text.Encodings.Web;
using System.Text.Json;
using little_heart_bot_3.Data;
using little_heart_bot_3.Services;
using little_heart_bot_3.Services.Implements;
using Microsoft.Extensions.Configuration;
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

        builder.Services.AddDbContext<LittleHeartDbContext>();
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

        builder.Services.AddScoped<IBotService, BotService>();
        builder.Services.AddScoped<IAppService, AppService>();

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