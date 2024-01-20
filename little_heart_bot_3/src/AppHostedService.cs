using little_heart_bot_3.Data;
using little_heart_bot_3.Others;
using little_heart_bot_3.ScheduleJobs;
using little_heart_bot_3.Services;
using Microsoft.EntityFrameworkCore;

namespace little_heart_bot_3;

public sealed class AppHostedService : BackgroundService
{
    private readonly ILogger _logger;
    private readonly IAppService _appService;
    private readonly IDbContextFactory<LittleHeartDbContext> _dbContextFactory;

    public AppHostedService(
        ILogger<AppHostedService> logger,
        IAppService appService,
        IDbContextFactory<LittleHeartDbContext> dbContextFactory)
    {
        _logger = logger;
        _dbContextFactory = dbContextFactory;
        _appService = appService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(stoppingToken);
            try
            {
                await _appService.VerifyCookiesAsync(stoppingToken);
                await _appService.SendMessageAsync(stoppingToken);
                await _appService.WatchLiveAsync(stoppingToken);
                Globals.AppStatus = AppStatus.Normal;
            }
            catch (LittleHeartException ex) when (ex.Reason == Reason.RiskControl)
            {
                Globals.AppStatus = AppStatus.Cooling;
                int cd = 15;
                while (cd != 0)
                {
                    _logger.LogError("请求过于频繁，还需冷却 {cd} 分钟", cd);
                    await Task.Delay(60 * 1000, stoppingToken);
                    cd--;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "出现预料之外的错误");
            }
            finally
            {
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}