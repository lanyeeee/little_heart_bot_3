using little_heart_bot_3.Data;
using little_heart_bot_3.Data.Models;
using little_heart_bot_3.Others;
using little_heart_bot_3.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace little_heart_bot_3;

public sealed class App : BackgroundService
{
    private readonly ILogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public App([FromKeyedServices("app:Logger")] ILogger logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var cancellationTokenSource = new CancellationTokenSource();

            await using var scope = _scopeFactory.CreateAsyncScope();
            var appService = scope.ServiceProvider.GetRequiredService<IAppService>();

            await using var db = new LittleHeartDbContext();
            try
            {
                await appService.VerifyCookiesAsync(cancellationTokenSource.Token);
                //TODO: 后续需要改用Semaphore
                List<UserModel> users = await db.Users.AsNoTracking()
                    .Include(u => u.Messages)
                    .Include(u => u.Targets)
                    .AsSplitQuery()
                    .Where(u => !u.Completed && u.CookieStatus == CookieStatus.Normal)
                    .Take(30)
                    .ToListAsync(cancellationTokenSource.Token);

                await appService.SendMessageAsync(users, cancellationTokenSource.Token);
                await appService.WatchLiveAsync(users, cancellationTokenSource.Token);
                Globals.AppStatus = AppStatus.Normal;
            }
            catch (LittleHeartException ex)
            {
                switch (ex.Reason)
                {
                    case Reason.Ban:
                        cancellationTokenSource.Cancel();
                        Globals.AppStatus = AppStatus.Cooling;
                        int cd = 15;
                        while (cd != 0)
                        {
                            _logger.Error("请求过于频繁，还需冷却 {cd} 分钟", cd);
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
                _logger.Fatal(ex, "出现预料之外的错误");
                Console.WriteLine(ex);
            }
            finally
            {
                await Task.Delay(5000, CancellationToken.None);
            }
        }
    }
}