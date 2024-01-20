using little_heart_bot_3.Data.Models;
using little_heart_bot_3.Others;
using little_heart_bot_3.Services;
using Quartz;

namespace little_heart_bot_3.ScheduleJobs;

public class UpdateSignJob : IJob
{
    private readonly IBotService _botService;
    private readonly BotModel _botModel;
    private readonly ILogger<BotHostedService> _logger;

    public UpdateSignJob(IBotService botService, IConfiguration configuration, ILogger<BotHostedService> logger)
    {
        _botService = botService;
        _logger = logger;
        _botModel = BotModel.LoadFromConfiguration(configuration);
    }

    public async Task Execute(IJobExecutionContext context)
    {
        if (Globals.BotStatus is not BotStatus.Normal)
        {
            return;
        }

        try
        {
            string sign = MakeSign();
            await _botService.UpdateSignAsync(_botModel, sign);
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
        catch (LittleHeartException ex) when (ex.Reason == Reason.Ban)
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