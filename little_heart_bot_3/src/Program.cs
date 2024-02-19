using System.Text.Encodings.Web;
using System.Text.Json;
using Coravel;
using little_heart_bot_3;
using little_heart_bot_3.Data;
using little_heart_bot_3.Others;
using little_heart_bot_3.ScheduleJobs;
using little_heart_bot_3.Services;
using little_heart_bot_3.Services.Implements;
using little_heart_bot_3.Services.Implements.App;
using little_heart_bot_3.Services.Implements.Bot;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Polly;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddJsonFile("AppData/appsettings.json", optional: false, reloadOnChange: true);

builder.Services.AddTransient<NewDayJob>();
builder.Services.AddTransient<UpdateSignJob>();
builder.Services.AddScheduler();

builder.Services.AddHttpClient("global")
    .AddTransientHttpErrorPolicy(policyBuilder => policyBuilder.WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: retryCount => retryCount * TimeSpan.FromSeconds(1),
        onRetry: (outcome, timespan, retryCount, context) =>
        {
            if (context.TryGetValue("callback", out var callbackObject) &&
                callbackObject is Action<DelegateResult<HttpResponseMessage>, TimeSpan, int> callback)
            {
                callback(outcome, timespan, retryCount);
            }
        }));

builder.Services.AddPooledDbContextFactory<LittleHeartDbContext>(options =>
    {
        var connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = builder.Configuration["Sqlite:DataSource"]!,
            DefaultTimeout = 10
        };

        options.UseSqlite(connectionStringBuilder.ConnectionString);
    }
);

builder.Services.AddSerilog(serilogConfig =>
{
    serilogConfig
        .MinimumLevel.Information()
        .WriteTo.Logger(botConfig =>
        {
            botConfig
                .Filter.ByIncludingOnly(evt => evt.SourceContextEquals(typeof(BotHostedService)))
                .MinimumLevel.Information()
                .WriteTo.File(
                    path: "AppData/logs/bot/bot-.clef",
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: 1 * 1024 * 1024,
                    rollOnFileSizeLimit: true,
                    buffered: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(1),
                    formatter: new CompactJsonFormatter());
        })
        .WriteTo.Logger(appConfig =>
        {
            appConfig
                .Filter.ByIncludingOnly(evt => evt.SourceContextEquals(typeof(AppHostedService)))
                .MinimumLevel.Information()
                .WriteTo.File(
                    path: "AppData/logs/app/app-.clef",
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: 2 * 1024 * 1024,
                    rollOnFileSizeLimit: true,
                    buffered: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(1),
                    formatter: new CompactJsonFormatter());
        });

    if (builder.Environment.IsDevelopment())
    {
        serilogConfig
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Information)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.Extensions.Http.DefaultHttpClientFactory", LogEventLevel.Information)
            .WriteTo.Console().MinimumLevel.Debug()
            .WriteTo.Logger(debugConfig =>
            {
                debugConfig
                    .MinimumLevel.Debug()
                    .WriteTo.File(
                        path: "logs/debug/debug.clef",
                        rollingInterval: RollingInterval.Infinite,
                        fileSizeLimitBytes: 100 * 1024 * 1024,
                        rollOnFileSizeLimit: true,
                        formatter: new CompactJsonFormatter());
            });
    }
});

builder.Services.AddSingleton<JsonSerializerOptions>(_ => new JsonSerializerOptions
{
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
});

builder.Services.AddSingleton<IBotService, BotService>();
builder.Services.AddSingleton<IAppService, AppService>();

//ApiService
builder.Services.AddKeyedSingleton<IApiService, BotApiService>("bot:ApiService");
builder.Services.AddKeyedSingleton<IApiService, AppApiService>("app:ApiService");
//MessageService
builder.Services.AddKeyedSingleton<IMessageService, BotMessageService>("bot:MessageService");
builder.Services.AddKeyedSingleton<IMessageService, AppMessageService>("app:MessageService");
//TargetService
builder.Services.AddKeyedSingleton<ITargetService, BotTargetService>("bot:TargetService");
builder.Services.AddKeyedSingleton<ITargetService, AppTargetService>("app:TargetService");
//UserService
builder.Services.AddKeyedSingleton<IUserService, BotUserService>("bot:UserService");
builder.Services.AddKeyedSingleton<IUserService, AppUserService>("app:UserService");
//EmailService
builder.Services.AddKeyedSingleton<IEmailService, AppEmailService>("bot:EmailService");
builder.Services.AddKeyedSingleton<IEmailService, BotEmailService>("app:EmailService");

builder.Services.AddHostedService<BotHostedService>();
builder.Services.AddHostedService<AppHostedService>();

var host = builder.Build();

host.Services.UseScheduler(scheduler =>
{
    scheduler.Schedule<NewDayJob>().DailyAt(16, 5);
    scheduler.Schedule<UpdateSignJob>().EveryMinute();
});

host.Run();