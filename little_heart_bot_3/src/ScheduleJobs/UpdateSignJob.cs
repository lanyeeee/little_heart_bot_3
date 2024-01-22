using Coravel.Invocable;
using little_heart_bot_3.Data.Models;
using little_heart_bot_3.Others;
using little_heart_bot_3.Services;

namespace little_heart_bot_3.ScheduleJobs;

public class UpdateSignJob : IInvocable
{
    private readonly IBotService _botService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BotHostedService> _logger;

    private static AppStatus? LastAppStatus { get; set; }
    private static BotStatus? LastBotStatus { get; set; }

    public UpdateSignJob(IBotService botService, IConfiguration configuration, ILogger<BotHostedService> logger)
    {
        _botService = botService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task Invoke()
    {
        if (Globals.BotStatus is not BotStatus.Normal)
        {
            return;
        }

        try
        {
            BotModel botModel = BotModel.LoadFromConfiguration(_configuration) ??
                                throw new LittleHeartException(Reason.BotCookieExpired);
            // 有任何一个状态发生变化，就更新签名
            if (LastAppStatus != Globals.AppStatus || LastBotStatus != Globals.BotStatus)
            {
                string sign = MakeSign();
                await _botService.UpdateSignAsync(botModel, sign);
                LastAppStatus = Globals.AppStatus;
                LastBotStatus = Globals.BotStatus;
            }
        }
        catch (OperationCanceledException)
        {
            //ignore
        }
        catch (LittleHeartException ex) when (ex.Reason == Reason.BotCookieExpired)
        {
            Globals.BotStatus = BotStatus.CookieExpired;
            //ignore
        }
        catch (LittleHeartException ex) when (ex.Reason == Reason.RiskControl)
        {
            //ignore
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "更新签名时遇到意料之外的错误");
        }
    }

    private static string MakeSign()
    {
        string appStatus = Globals.AppStatus switch
        {
            null => "弹幕、点赞、观看直播状态检测中",
            AppStatus.Normal => "弹幕、点赞、观看直播正常",
            AppStatus.Cooling => "弹幕、点赞、观看直播冷却中",
            _ => ""
        };
        string botStatus = Globals.BotStatus switch
        {
            null => "收发私信状态检测中",
            BotStatus.Normal => "收发私信正常",
            BotStatus.Cooling => "收发冷却中",
            BotStatus.CookieExpired => "收发私信异常",
            _ => ""
        };

        return $"给你【{appStatus}，{botStatus}】";
    }
}

public enum AppStatus
{
    Normal = 0,
    Cooling = -1
}

public enum BotStatus
{
    Normal = 0,
    Cooling = -1,
    CookieExpired = -2,
}