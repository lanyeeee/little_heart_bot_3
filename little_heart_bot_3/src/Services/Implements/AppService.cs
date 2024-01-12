using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using little_heart_bot_3.Data;
using little_heart_bot_3.Data.Models;
using little_heart_bot_3.Others;
using Microsoft.EntityFrameworkCore;
using Polly;

namespace little_heart_bot_3.Services.Implements;

public class AppService : IAppService
{
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _options;
    private readonly HttpClient _httpClient;
    private readonly IUserService _userService;
    private readonly IDbContextFactory<LittleHeartDbContext> _dbContextFactory;


    public AppService([FromKeyedServices("app:Logger")] ILogger logger,
        JsonSerializerOptions options,
        IHttpClientFactory httpclientFactory,
        [FromKeyedServices("app:UserService")] IUserService userService,
        IDbContextFactory<LittleHeartDbContext> dbContextFactory)
    {
        _logger = logger;
        _options = options;
        _httpClient = httpclientFactory.CreateClient("global");
        _userService = userService;
        _dbContextFactory = dbContextFactory;
    }

    public async Task VerifyCookiesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
        List<UserModel> users = await db.Users
            .Include(u => u.Messages)
            .Include(u => u.Targets)
            .AsSplitQuery()
            .Where(u => u.CookieStatus == CookieStatus.Unverified)
            .ToListAsync(cancellationToken);

        foreach (var user in users)
        {
            try
            {
                var requestMessage = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri("https://api.bilibili.com/x/web-interface/nav"),
                    Headers = { { "Cookie", user.Cookie } }
                }.SetRetryCallback((outcome, retryDelay, retryCount) =>
                {
                    _logger.LogWarning(outcome.Exception,
                        "uid {Uid} 验证cookie时遇到异常，准备在 {RetryDelay} 秒后进行第 {RetryCount} 次重试",
                        user.Uid,
                        retryDelay.TotalSeconds,
                        retryCount);
                });

                HttpResponseMessage responseMessage = await _httpClient.SendAsync(requestMessage, cancellationToken);
                await Task.Delay(1000, cancellationToken);
                var response =
                    (await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, cancellationToken))!;

                int? code = (int?)response["code"];
                if (code == 0)
                {
                    user.CookieStatus = CookieStatus.Normal;
                    await db.SaveChangesAsync(CancellationToken.None);
                }
                else if (code == -412)
                {
                    _logger.LogWithResponse(
                        () => _logger.LogWarning("uid {Uid} 验证cookie的请求被拦截", user.Uid),
                        response.ToJsonString(_options));

                    _logger.LogWarning("uid {Uid} 验证cookie的请求被拦截", user.Uid);
                    throw new LittleHeartException(Reason.Ban);
                }
                else if (code == -101)
                {
                    _logger.LogWithResponse(
                        () => _logger.LogInformation("uid {uid} 提供的cookie错误或已过期", user.Uid),
                        response.ToJsonString(_options));

                    throw new LittleHeartException(Reason.CookieExpired);
                }
                else
                {
                    _logger.LogWithResponse(
                        () => _logger.LogError("uid {uid} 验证cookie时出现预料之外的错误", user.Uid),
                        response.ToJsonString(_options));
                    user.CookieStatus = CookieStatus.Error;
                    await db.SaveChangesAsync(CancellationToken.None);
                }
            }
            catch (LittleHeartException ex)
            {
                switch (ex.Reason)
                {
                    case Reason.Ban:
                        throw;
                    case Reason.CookieExpired:
                        user.CookieStatus = CookieStatus.Error;
                        await db.SaveChangesAsync(CancellationToken.None);
                        break;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex,
                    "uid {Uid} 验证cookie时出现 HttpRequestException 异常，polly尝试多次后依旧发生异常",
                    user.Uid);
                user.CookieStatus = CookieStatus.Error;
                await db.SaveChangesAsync(CancellationToken.None);
                throw new LittleHeartException(Reason.Ban);
            }
        }
    }

    public async Task SendMessageAsync(List<UserModel> users, CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>();

        foreach (var user in users)
        {
            tasks.Add(_userService.SendMessageAsync(user, cancellationToken));
            await Task.Delay(100, cancellationToken);
        }

        while (tasks.Count != 0)
        {
            Task completedTask = await Task.WhenAny(tasks);
            tasks.Remove(completedTask);

            await completedTask;
        }
    }

    public async Task WatchLiveAsync(List<UserModel> users, CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>();
        foreach (var user in users)
        {
            tasks.Add(_userService.WatchLiveAsync(user, cancellationToken));
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