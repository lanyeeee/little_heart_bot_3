using System.Text.Encodings.Web;
using System.Text.Json;
using little_heart_bot_3;
using little_heart_bot_3.Data;
using little_heart_bot_3.Services;
using little_heart_bot_3.Services.Implements;
using little_heart_bot_3.Services.Implements.App;
using little_heart_bot_3.Services.Implements.Bot;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Serilog;
using Serilog.Enrichers.WithCaller;
using Serilog.Templates;
using ILogger = Microsoft.Extensions.Logging.ILogger;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<LittleHeartDbContext>(options =>
    {
        var connectionStringBuilder = new MySqlConnectionStringBuilder
        {
            Server = builder.Configuration["MYSQL:host"],
            Database = builder.Configuration["MYSQL:database"],
            UserID = builder.Configuration["MYSQL:user"],
            Password = builder.Configuration["MYSQL:password"]
        };

        var serverVersion = ServerVersion.AutoDetect(connectionStringBuilder.ConnectionString);

        options.UseMySql(connectionStringBuilder.ConnectionString, serverVersion);
    },
    ServiceLifetime.Transient
);
builder.Services.AddSingleton<HttpClient>();
builder.Services.AddSingleton<JsonSerializerOptions>(_ => new JsonSerializerOptions
{
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
});

var logFormatter = new ExpressionTemplate("{ {ts:@t, template:@mt, msg:@m, level:@l, ex:@x, p:{..@p}} }\n");

builder.Services.AddKeyedSingleton<ILogger>("bot:Logger", (_, _) =>
{
    var serilog = new LoggerConfiguration()
        .MinimumLevel.Verbose()
        .WriteTo.File(
            path: "logs/bot/bot-.txt",
            rollingInterval: RollingInterval.Day,
            fileSizeLimitBytes: 1 * 1024 * 1024,
            rollOnFileSizeLimit: true,
            buffered: true,
            flushToDiskInterval: TimeSpan.FromSeconds(10),
            formatter: logFormatter)
        .Enrich.WithCaller(true)
        .CreateLogger();
    return new Logger<Serilog.ILogger>(LoggerFactory.Create(loggerBuilder => loggerBuilder.AddSerilog(serilog)));
});

builder.Services.AddKeyedSingleton<ILogger>("app:Logger", (_, _) =>
{
    var serilog = new LoggerConfiguration()
        .MinimumLevel.Verbose()
        .WriteTo.File(
            path: "logs/app/app-.txt",
            rollingInterval: RollingInterval.Day,
            fileSizeLimitBytes: 2 * 1024 * 1024,
            rollOnFileSizeLimit: true,
            buffered: true,
            flushToDiskInterval: TimeSpan.FromSeconds(1),
            formatter: logFormatter)
        .Enrich.WithCaller(true)
        .CreateLogger();
    return new Logger<Serilog.ILogger>(LoggerFactory.Create(loggerBuilder => loggerBuilder.AddSerilog(serilog)));
});

builder.Services.AddSingleton<IBotService, BotService>();
builder.Services.AddSingleton<IAppService, AppService>();

//AppMessageService
builder.Services.AddKeyedSingleton<IMessageService, BotMessageService>("bot:MessageService");
builder.Services.AddKeyedSingleton<IMessageService, AppMessageService>("app:MessageService");
//BotTargetService
builder.Services.AddKeyedSingleton<ITargetService, BotTargetService>("bot:TargetService");
builder.Services.AddKeyedSingleton<ITargetService, AppTargetService>("app:TargetService");
//BotUserService
builder.Services.AddKeyedSingleton<IUserService, BotUserService>("bot:UserService");
builder.Services.AddKeyedSingleton<IUserService, AppUserService>("app:UserService");

builder.Services.AddHostedService<BotHostedService>();
builder.Services.AddHostedService<AppHostedService>();

var host = builder.Build();

host.Run();