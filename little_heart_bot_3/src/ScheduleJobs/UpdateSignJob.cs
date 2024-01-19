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
        try
        {
            await _botService.UpdateSignAsync(_botModel);
        }
        catch (OperationCanceledException)
        {
            //ignore
        }
        catch (LittleHeartException ex) when (ex.Reason == Reason.BotCookieExpired)
        {
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
}