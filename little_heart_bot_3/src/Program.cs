using System.Text.Encodings.Web;
using System.Text.Json;
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
using Quartz;
using Serilog;
using Serilog.Enrichers.WithCaller;
using Serilog.Formatting.Compact;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddQuartz(quartzConfig =>
{
    var neyDayJobKey = new JobKey("NewDayJob");
    quartzConfig.AddJob<NewDayJob>(configurator => configurator.WithIdentity(neyDayJobKey));
    quartzConfig.AddTrigger(configurator =>
    {
        configurator.ForJob(neyDayJobKey)
            .WithIdentity("NewDayJobTrigger")
            .WithCronSchedule("0 5 0 1/1 * ? *");
    });

    var updateSignJobKey = new JobKey("UpdateSignJob");
    quartzConfig.AddJob<UpdateSignJob>(configurator => configurator.WithIdentity(updateSignJobKey));
    quartzConfig.AddTrigger(configurator =>
    {
        configurator.ForJob(updateSignJobKey)
            .WithIdentity("UpdateSignJobTrigger")
            .WithCronSchedule("0 0/1 * 1/1 * ? *");
    });
});
builder.Services.AddQuartzHostedService(quartzConfig => quartzConfig.WaitForJobsToComplete = true);

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
            DataSource = builder.Configuration["Sqlite:DataSource"]!
        };

        options.UseSqlite(connectionStringBuilder.ConnectionString);
    }
);

builder.Services.AddSerilog(serilogConfig =>
{
    serilogConfig
        .MinimumLevel.Verbose()
        .WriteTo.Logger(botConfig =>
        {
            botConfig
                .Filter.ByIncludingOnly(evt => evt.SourceContextEquals(typeof(BotHostedService)))
                .MinimumLevel.Information()
                .Enrich.WithCaller()
                .WriteTo.File(
                    path: "logs/bot/bot-.clef",
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: 1 * 1024 * 1024,
                    rollOnFileSizeLimit: true,
                    buffered: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(10),
                    formatter: new CompactJsonFormatter());
        })
        .WriteTo.Logger(appConfig =>
        {
            appConfig
                .Filter.ByIncludingOnly(evt => evt.SourceContextEquals(typeof(AppHostedService)))
                .MinimumLevel.Information()
                .Enrich.WithCaller()
                .WriteTo.File(
                    path: "logs/app/app-.clef",
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
            .WriteTo.Console().MinimumLevel.Verbose()
            .WriteTo.Logger(debugConfig =>
            {
                debugConfig
                    .MinimumLevel.Verbose()
                    .Enrich.WithCaller()
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