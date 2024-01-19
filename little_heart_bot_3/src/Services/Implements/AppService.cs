using System.Text.Json;
using little_heart_bot_3.Data;
using little_heart_bot_3.Data.Models;
using little_heart_bot_3.Others;
using Microsoft.EntityFrameworkCore;

namespace little_heart_bot_3.Services.Implements;

public class AppService : IAppService
{
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _options;
    private readonly IApiService _apiService;
    private readonly IUserService _userService;
    private readonly IDbContextFactory<LittleHeartDbContext> _dbContextFactory;


    public AppService(ILogger<AppHostedService> logger,
        JsonSerializerOptions options,
        [FromKeyedServices("app:ApiService")] IApiService apiService,
        [FromKeyedServices("app:UserService")] IUserService userService,
        IDbContextFactory<LittleHeartDbContext> dbContextFactory)
    {
        _logger = logger;
        _options = options;
        _userService = userService;
        _dbContextFactory = dbContextFactory;
        _apiService = apiService;
    }

    public async Task VerifyCookiesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var users = db.Users
            .Include(u => u.Messages)
            .Include(u => u.Targets)
            .AsSplitQuery()
            .Where(u => u.CookieStatus == CookieStatus.Unverified);

        foreach (var user in users)
        {
            try
            {
                var response = await _apiService.VerifyCookiesAsync(user, cancellationToken);

                bool valid;
                int? code = (int?)response["code"];
                switch (code)
                {
                    case 0:
                        _logger.LogDebug("uid {Uid} 验证cookie成功", user.Uid);
                        valid = true;
                        break;
                    case -412:
                        _logger.LogWithResponse(
                            () => _logger.LogWarning("uid {Uid} 验证cookie的请求被拦截", user.Uid),
                            response.ToJsonString(_options));
                        throw new LittleHeartException(Reason.Ban);
                    case -101:
                        _logger.LogWithResponse(
                            () => _logger.LogInformation("uid {uid} 提供的cookie错误或已过期", user.Uid),
                            response.ToJsonString(_options));
                        valid = false;
                        break;
                    default:
                        _logger.LogWithResponse(
                            () => _logger.LogError("uid {uid} 验证cookie时出现预料之外的错误", user.Uid),
                            response.ToJsonString(_options));
                        valid = false;
                        break;
                }

                user.CookieStatus = valid ? CookieStatus.Normal : CookieStatus.Error;
                await db.SaveChangesAsync(cancellationToken);
                await Task.Delay(1000, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex,
                    "uid {Uid} 验证cookie时出现 HttpRequestException 异常，重试多次后依旧发生异常",
                    user.Uid);
                throw new LittleHeartException(ex.Message, ex, Reason.Ban);
            }
        }
    }

    public async Task SendMessageAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var users = db.Users.AsNoTracking()
            .Include(u => u.Messages)
            .Include(u => u.Targets)
            .AsSplitQuery()
            .Where(u => !u.Completed && u.CookieStatus == CookieStatus.Normal);

        var tasks = new List<Task>();
        var semaphore = new SemaphoreSlim(10);

        foreach (var user in users)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // 如果同时运行的任务数量达到上限，等待任意任务完成
            if (semaphore.CurrentCount == 0)
            {
                Task completedTask = await Task.WhenAny(tasks);
                tasks.Remove(completedTask);
                await completedTask;
            }

            // 启动新的消息发送任务
            var task = Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    await _userService.SendMessageAsync(user, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);

            tasks.Add(task);
            await Task.Delay(100, cancellationToken);
        }

        while (tasks.Count != 0)
        {
            Task completedTask = await Task.WhenAny(tasks);
            tasks.Remove(completedTask);
            await completedTask;
        }
    }

    public async Task WatchLiveAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var users = db.Users.AsNoTracking()
            .Include(u => u.Messages)
            .Include(u => u.Targets)
            .AsSplitQuery()
            .Where(u => !u.Completed && u.CookieStatus == CookieStatus.Normal);

        var tasks = new List<Task>();
        var semaphore = new SemaphoreSlim(10);
        foreach (var user in users)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // 如果同时运行的任务数量达到上限，等待任意任务完成
            if (semaphore.CurrentCount == 0)
            {
                Task completedTask = await Task.WhenAny(tasks);
                tasks.Remove(completedTask);
                await completedTask;
            }

            // 启动新的消息发送任务
            var task = Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    await _userService.WatchLiveAsync(user, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);

            tasks.Add(task);
            await Task.Delay(2000, cancellationToken);
        }

        while (tasks.Count != 0)
        {
            Task completedTask = await Task.WhenAny(tasks);
            tasks.Remove(completedTask);
            await completedTask;
        }
    }
}