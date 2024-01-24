using System.Text.Json.Nodes;
using little_heart_bot_3.Data;
using little_heart_bot_3.Data.Models;
using little_heart_bot_3.Others;
using little_heart_bot_3.ScheduleJobs;
using little_heart_bot_3.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace little_heart_bot_3;

public sealed class BotHostedService : BackgroundService
{
    private readonly ILogger _logger;
    private readonly IBotService _botService;
    private readonly IDbContextFactory<LittleHeartDbContext> _dbContextFactory;
    private readonly IConfiguration _configuration;

    private BotModel _botModel = null!;
    private readonly SemaphoreSlim _botStatusSemaphore;

    public BotHostedService(
        ILogger<BotHostedService> logger,
        IBotService botService,
        IDbContextFactory<LittleHeartDbContext> dbContextFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _botService = botService;
        _dbContextFactory = dbContextFactory;
        _configuration = configuration;
        _botStatusSemaphore = new SemaphoreSlim(0, 1);
        ChangeToken.OnChange(
            () => _configuration.GetReloadToken(),
            () =>
            {
                Globals.BotStatus = null;
                if (_botStatusSemaphore.CurrentCount == 0)
                {
                    _botStatusSemaphore.Release();
                }
            });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (Globals.BotStatus is BotStatus.CookieExpired)
                {
                    _logger.LogError("Bot的Cookie已过期，Bot主线程阻塞，直到管理员手动更新Cookie到appsettings.json");

                    if (_botStatusSemaphore.CurrentCount == 1)
                    {
                        await _botStatusSemaphore.WaitAsync(stoppingToken);
                    }

                    await _botStatusSemaphore.WaitAsync(stoppingToken);

                    _logger.LogError("Bot的阻塞已解除，继续运行");
                }

                _botModel = BotModel.LoadFromConfiguration(_configuration) ??
                            throw new LittleHeartException(Reason.BotCookieExpired);

                await HandleIncomingPrivateMessageAsync(stoppingToken);

                Globals.BotStatus = BotStatus.Normal;
            }
            catch (LittleHeartException ex) when (ex.Reason == Reason.RiskControl)
            {
                await CoolingDown(stoppingToken);
            }
            catch (LittleHeartException ex) when (ex.Reason == Reason.BotCookieExpired)
            {
                Globals.BotStatus = BotStatus.CookieExpired;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "遇到了意料之外的错误");
            }
            finally
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogWarning("BotHostedService已退出");
        return base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <exception cref="OperationCanceledException"></exception>
    private async Task CoolingDown(CancellationToken cancellationToken = default)
    {
        int cd = 15;
        Globals.BotStatus = BotStatus.Cooling;
        while (cd != 0)
        {
            _logger.LogWarning("遇到风控 还需冷却 {cd} 分钟", cd);
            await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            cd--;
        }

        Globals.BotStatus = null;
    }

    /// <summary>
    /// 处理lastTimestamp之后的私信
    /// </summary>
    /// <param name="user"></param>
    /// <param name="lastTimestamp"></param>
    /// <param name="privateMessages"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="LittleHeartException">
    /// <br/>Reason.RiskControl
    /// </exception>
    private async Task HandlePrivateMessagesAsync(
        UserModel user,
        long lastTimestamp,
        IEnumerable<JsonNode?> privateMessages,
        CancellationToken cancellationToken = default)
    {
        foreach (JsonNode? msg in privateMessages)
        {
            //忽略 已读的、非本人发送的、非文字的 消息
            if (msg is null ||
                (long?)msg["timestamp"] <= lastTimestamp ||
                (long?)msg["sender_uid"] != user.Uid ||
                (int?)msg["msg_type"] != 1)
            {
                continue;
            }


            long timestamp = (long)msg["timestamp"]!;
            string contentJson = (string)msg["content"]!;

            user.ReadTimestamp = timestamp;

            string? content = JsonNode.Parse(contentJson)!["content"]?.GetValue<string>().Trim();
            if (string.IsNullOrEmpty(content) || !content.StartsWith('/'))
            {
                continue;
            }

            _logger.LogInformation("{Uid}：{Content}", user.Uid, content);

            string[] pair = content.Split(" ", 2);
            string command = pair[0].Trim();
            string? parameter = pair.Length == 2 ? pair[1].Trim() : null;

            await _botService.HandleCommandAsync(_botModel, user, command, parameter, cancellationToken);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="LittleHeartException">
    /// <br/>Reason.RiskControl
    /// </exception>
    private async Task HandleIncomingPrivateMessageAsync(CancellationToken cancellationToken = default)
    {
        IEnumerable<JsonNode?>? sessionList =
            await _botService.GetSessionListAsync(_botModel, cancellationToken);
        if (sessionList is null)
        {
            return;
        }

        //TODO: 需要为每个session进行一次SQL查询，导致GC频繁，后续看看能不能优化
        foreach (var session in sessionList)
        {
            if (session?["last_msg"] is null)
            {
                continue;
            }

            long uid = (long)session["talker_id"]!;
            JsonObject lastMsg = session["last_msg"]!.AsObject();
            long timestamp = lastMsg.Count != 0 ? (long)lastMsg["timestamp"]! : 0;

            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var readTimestamp = await db.Users.AsNoTracking()
                .Where(u => u.Uid == uid)
                .Select(u => (long?)u.ReadTimestamp)
                .FirstOrDefaultAsync(cancellationToken);

            if (readTimestamp is null) //新用户
            {
                var user = new UserModel
                {
                    Uid = uid,
                    Cookie = string.Empty,
                    Csrf = string.Empty,
                    CookieStatus = CookieStatus.Error,
                    ReadTimestamp = timestamp,
                    ConfigTimestamp = 0,
                    ConfigNum = 0
                };
                await db.Users.AddAsync(user, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                IEnumerable<JsonNode?>? privateMessages =
                    await _botService.GetPrivateMessagesAsync(_botModel, user, cancellationToken);

                if (privateMessages is not null)
                {
                    await HandlePrivateMessagesAsync(user, 0, privateMessages, cancellationToken);
                }
            }
            else if (timestamp > readTimestamp) //发新消息的用户
            {
                var user = await db.Users
                    .Include(u => u.Targets)
                    .Include(u => u.Messages)
                    .AsSplitQuery()
                    .FirstAsync(u => u.Uid == uid, cancellationToken);
                db.Attach(user);
                IEnumerable<JsonNode?>? privateMessages =
                    await _botService.GetPrivateMessagesAsync(_botModel, user, cancellationToken);

                //只要成功获取用户的私信，无论这些私信是否成功处理，都只处理一次
                user.ReadTimestamp = timestamp;

                if (privateMessages is not null)
                {
                    await HandlePrivateMessagesAsync(user, readTimestamp.Value, privateMessages, cancellationToken);
                }
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }
}