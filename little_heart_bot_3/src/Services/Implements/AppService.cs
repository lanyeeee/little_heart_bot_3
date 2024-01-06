using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using little_heart_bot_3.Data;
using little_heart_bot_3.Data.Models;
using little_heart_bot_3.Others;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Retry;
using Serilog;

namespace little_heart_bot_3.Services.Implements;

public class AppService : IAppService
{
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _options;
    private readonly HttpClient _httpClient;

    private readonly ResiliencePipeline _verifyCookiesPipeline;

    public AppService(
        [FromKeyedServices("app:Logger")] ILogger logger,
        JsonSerializerOptions options,
        HttpClient httpClient)
    {
        _logger = logger;
        _options = options;
        _httpClient = httpClient;

        _verifyCookiesPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<LittleHeartException>(ex => ex.Reason == Reason.NullResponse)
                    .Handle<HttpRequestException>(),
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                MaxRetryAttempts = 5,
                OnRetry = args =>
                {
                    var user = args.Context.Properties.GetValue(LittleHeartResilienceKeys.User, null)!;
                    _logger.Warning(args.Outcome.Exception,
                        "uid {Uid} 验证cookie时遇到异常，准备在 {RetryDelay} 秒后进行第 {AttemptNumber} 次重试",
                        user.Uid,
                        args.RetryDelay.TotalSeconds,
                        args.AttemptNumber);
                    return default;
                }
            })
            .Build();
    }

    public async Task VerifyCookiesAsync(CancellationToken cancellationToken)
    {
        await using var db = new LittleHeartDbContext();
        List<UserModel> users = await db.Users
            .Include(u => u.Messages)
            .Include(u => u.Targets)
            .AsSplitQuery()
            .Where(u => u.CookieStatus == CookieStatus.Unverified)
            .ToListAsync(cancellationToken);
        foreach (var user in users)
        {
            var context = ResilienceContextPool.Shared.Get(cancellationToken);
            context.Properties.Set(LittleHeartResilienceKeys.User, user);

            try
            {
                await _verifyCookiesPipeline.ExecuteAsync(async ctx =>
                {
                    HttpResponseMessage responseMessage = await _httpClient.SendAsync(new HttpRequestMessage
                    {
                        Method = HttpMethod.Get,
                        RequestUri = new Uri("https://api.bilibili.com/x/web-interface/nav"),
                        Headers = { { "Cookie", user.Cookie } }
                    }, ctx.CancellationToken);
                    await Task.Delay(1000, ctx.CancellationToken);

                    JsonNode response =
                        await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options,
                            ctx.CancellationToken) ??
                        throw new LittleHeartException(Reason.NullResponse);

                    int? code = (int?)response["code"];
                    if (code == 0)
                    {
                        user.CookieStatus = CookieStatus.Normal;
                        await db.SaveChangesAsync(CancellationToken.None);
                    }
                    else if (code == -412)
                    {
                        _logger.ForContext("Response", response.ToJsonString(_options))
                            .Warning("uid {Uid} 验证cookie的请求被拦截", user.Uid);
                        throw new LittleHeartException(Reason.Ban);
                    }
                    else if (code == -101)
                    {
                        _logger.ForContext("Response", response.ToJsonString(_options))
                            .Information("uid {uid} 提供的cookie错误或已过期", user.Uid);
                        throw new LittleHeartException(Reason.CookieExpired);
                    }
                    else
                    {
                        _logger.ForContext("Response", response.ToJsonString(_options))
                            .Error("uid {uid} 验证cookie时出现预料之外的错误", user.Uid);
                        user.CookieStatus = CookieStatus.Error;
                        await db.SaveChangesAsync(CancellationToken.None);
                    }
                }, context);
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
                    case Reason.NullResponse:
                        _logger.Error(ex,
                            "uid {Uid} 验证cookie时出现 NullResponse 异常，polly尝试多次后依旧发生异常",
                            user.Uid);
                        user.CookieStatus = CookieStatus.Error;
                        await db.SaveChangesAsync(CancellationToken.None);
                        break;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.Error(ex,
                    "uid {Uid} 验证cookie时出现 HttpRequestException 异常，polly尝试多次后依旧发生异常",
                    user.Uid);
                user.CookieStatus = CookieStatus.Error;
                await db.SaveChangesAsync(CancellationToken.None);
                throw new LittleHeartException(Reason.Ban);
            }
            finally
            {
                ResilienceContextPool.Shared.Return(context);
            }
        }
    }

    public async Task SendMessageAsync(List<UserModel> users, CancellationToken cancellationToken)
    {
        //TODO 2:
        throw new NotImplementedException();
    }

    public async Task WatchLiveAsync(List<UserModel> users, CancellationToken cancellationToken)
    {
        //TODO 2:
        throw new NotImplementedException();
    }
}