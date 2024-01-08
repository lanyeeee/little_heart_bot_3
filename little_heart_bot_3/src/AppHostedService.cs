using little_heart_bot_3.Data;
using little_heart_bot_3.Data.Models;
using little_heart_bot_3.Others;
using little_heart_bot_3.Services;
using Microsoft.EntityFrameworkCore;

namespace little_heart_bot_3;

public sealed class AppHostedService : BackgroundService
{
    private readonly ILogger _logger;
    private readonly IServiceProvider _provider;
    private readonly IAppService _appService;

    public AppHostedService([FromKeyedServices("app:Logger")] ILogger logger,
        IAppService appService,
        IServiceProvider provider)
    {
        _logger = logger;
        _provider = provider;
        _appService = appService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var cancellationTokenSource = new CancellationTokenSource();

            await using var db = _provider.GetRequiredService<LittleHeartDbContext>();
            try
            {
                await _appService.VerifyCookiesAsync(cancellationTokenSource.Token);
                //TODO: 后续需要改用Semaphore
                List<UserModel> users = await db.Users.AsNoTracking()
                    .Include(u => u.Messages)
                    .Include(u => u.Targets)
                    .AsSplitQuery()
                    .Where(u => !u.Completed && u.CookieStatus == CookieStatus.Normal)
                    .OrderBy(u => u.Id)
                    .Take(30)
                    .ToListAsync(cancellationTokenSource.Token);

                await _appService.SendMessageAsync(users, cancellationTokenSource.Token);
                await _appService.WatchLiveAsync(users, cancellationTokenSource.Token);
                Globals.AppStatus = AppStatus.Normal;
            }
            catch (LittleHeartException ex)
            {
                switch (ex.Reason)
                {
                    case Reason.Ban:
                        await cancellationTokenSource.CancelAsync();
                        Globals.AppStatus = AppStatus.Cooling;
                        int cd = 15;
                        while (cd != 0)
                        {
                            _logger.LogError("请求过于频繁，还需冷却 {cd} 分钟", cd);
                            await Task.Delay(60 * 1000, CancellationToken.None);
                            cd--;
                        }

                        break;
                }
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "出现预料之外的错误");
                Console.WriteLine(ex);
            }
            finally
            {
                await Task.Delay(5000, CancellationToken.None);
            }
        }
    }
}