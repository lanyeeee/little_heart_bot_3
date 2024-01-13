using System.Text.Json.Nodes;
using little_heart_bot_3.Data;
using little_heart_bot_3.Data.Models;
using little_heart_bot_3.Others;
using little_heart_bot_3.Services;
using Microsoft.EntityFrameworkCore;

namespace little_heart_bot_3;

public class BotHostedService : BackgroundService
{
    private readonly ILogger _logger;
    private readonly IBotService _botService;
    private readonly IDbContextFactory<LittleHeartDbContext> _dbContextFactory;

    private readonly BotModel _botModel;

    public BotHostedService(
        [FromKeyedServices("bot:Logger")] ILogger logger,
        IBotService botService,
        IDbContextFactory<LittleHeartDbContext> dbContextFactory)
    {
        _logger = logger;
        _botService = botService;
        _dbContextFactory = dbContextFactory;

        using var db = _dbContextFactory.CreateDbContext();
        BotModel? botModel = db.Bots.SingleOrDefault();
        if (botModel is null)
        {
            _logger.LogError("数据库bot_table表中没有数据");
            throw new LittleHeartException("数据库bot_table表中没有数据，请自行添加");
        }

        _botModel = botModel;

        Globals.AppStatus = _botModel.AppStatus;
        Globals.SendStatus = _botModel.SendStatus;
        Globals.ReceiveStatus = _botModel.ReceiveStatus;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            try
            {
                await HandleIncomingPrivateMessageAsync(cancellationTokenSource.Token);

                Globals.ReceiveStatus = ReceiveStatus.Normal;
            }
            catch (LittleHeartException ex)
            {
                switch (ex.Reason)
                {
                    case Reason.Ban:
                        await cancellationTokenSource.CancelAsync();

                        int cd = 15;
                        Globals.SendStatus = SendStatus.Cooling;
                        Globals.ReceiveStatus = ReceiveStatus.Cooling;
                        while (cd != 0)
                        {
                            _logger.LogWarning("遇到风控 还需冷却 {cd} 分钟", cd);
                            await Task.Delay(60 * 1000, CancellationToken.None);
                            cd--;
                        }

                        break;
                    case Reason.CookieExpired:
                        //TODO: 目前如果小心心bot的cookie过期，直接结束BotHostedService，后续要支持cookie热更新
                        return;
                    default:
                        _logger.LogCritical(ex, "这种情况不应该发生，如果发生了就是代码编写有问题");
                        break;
                }
            }
            catch (TaskCanceledException)
            {
                //ignore
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "遇到了意料之外的错误");
            }
            finally
            {
                await cancellationTokenSource.CancelAsync();
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    /// <summary>
    /// 处理lastTimestamp之后的私信
    /// </summary>
    /// <param name="user"></param>
    /// <param name="lastTimestamp"></param>
    /// <param name="privateMessages"></param>
    /// <param name="cancellationToken"></param>
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

            try
            {
                long timestamp = (long)msg["timestamp"]!;
                string contentJson = (string?)msg["content"]!;

                user.ReadTimestamp = timestamp;

                string? content = JsonNode.Parse(contentJson)!["content"]?.GetValue<string>().Trim();
                if (string.IsNullOrEmpty(content) || !content.StartsWith('/'))
                {
                    continue;
                }

                _logger.LogInformation("{Uid}：{Content}", user.Uid, content);
#if DEBUG
                Console.WriteLine($"{user.Uid}：{content}");
#endif
                string[] pair = content.Split(" ", 2);
                string command = pair[0].Trim();
                string? parameter = pair.Length == 2 ? pair[1].Trim() : null;

                await _botService.HandleCommandAsync(_botModel, user, command, parameter, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "处理uid {Uid} 的消息时出错", user.Uid);
            }
        }
    }

    private async Task HandleIncomingPrivateMessageAsync(CancellationToken cancellationToken = default)
    {
        IEnumerable<JsonNode?>? sessionList = await _botService.GetSessionListAsync(_botModel, cancellationToken);
        if (sessionList is null)
        {
            return;
        }

        //TODO: 需要为每个session进行一次SQL查询，导致性能较差，内存占用高，后续看看能不能优化
        foreach (var session in sessionList)
        {
            if (session is null)
            {
                continue;
            }

            long uid = (long)session["talker_id"]!;
            JsonObject lastMsg = session["last_msg"]!.AsObject();
            long timestamp = lastMsg.Count != 0 ? (long)lastMsg["timestamp"]! : 0;

            await using var db = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
            var user = await db.Users
                .Include(u => u.Messages)
                .Include(u => u.Targets)
                .AsSplitQuery()
                .FirstOrDefaultAsync(u => u.Uid == uid, cancellationToken);

            if (user is null) //新用户
            {
                user = new UserModel
                {
                    Uid = uid,
                    Cookie = string.Empty,
                    Csrf = string.Empty,
                    ReadTimestamp = timestamp,
                    ConfigTimestamp = 0,
                    ConfigNum = 0
                };
                IEnumerable<JsonNode?>? privateMessages =
                    await _botService.GetPrivateMessagesAsync(_botModel, user, cancellationToken);

                if (privateMessages is not null)
                {
                    await HandlePrivateMessagesAsync(user, 0, privateMessages, cancellationToken);
                }

                await db.AddAsync(user, CancellationToken.None);
            }
            else if (timestamp > user.ReadTimestamp) //发新消息的用户
            {
                IEnumerable<JsonNode?>? privateMessages =
                    await _botService.GetPrivateMessagesAsync(_botModel, user, cancellationToken);

                //只要成功获取用户的私信，无论这些私信是否成功处理，都只处理一次
                long readTimestamp = user.ReadTimestamp;
                user.ReadTimestamp = timestamp;

                if (privateMessages is not null)
                {
                    await HandlePrivateMessagesAsync(user, readTimestamp, privateMessages, cancellationToken);
                }
            }

            await db.SaveChangesAsync(CancellationToken.None);
        }
    }
}