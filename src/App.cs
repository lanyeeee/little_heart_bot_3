using System.Text.Json;
using System.Text.Json.Nodes;
using little_heart_bot_3.Models;
using little_heart_bot_3.Others;
using little_heart_bot_3.Services;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Retry;
using Serilog.Core;

namespace little_heart_bot_3;

public class App
{
    private readonly Logger _logger;
    private readonly IUserService _userService;

    private readonly JsonSerializerOptions _options;
    private readonly ResiliencePipeline _verifyCookiesPipeline;

    public App([FromKeyedServices("app:Logger")] Logger logger,
        [FromKeyedServices("app:UserService")] IUserService userService)
    {
        _logger = logger;
        _userService = userService;
        _options = Globals.JsonSerializerOptions;

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

    public async Task Main()
    {
        while (true)
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            try
            {
                //TODO: 后续需要改用Semaphore
                await VerifyCookiesAsync(cancellationTokenSource.Token);
                List<UserModel> users = await _userService.GetUncompletedUsersAsync(30, cancellationTokenSource.Token);
                await SendMessageAsync(users, cancellationTokenSource.Token);
                await WatchLiveAsync(users, cancellationTokenSource.Token);
                Globals.AppStatus = 0;
            }
            catch (LittleHeartException ex)
            {
                switch (ex.Reason)
                {
                    case Reason.Ban:
                        cancellationTokenSource.Cancel();
                        Globals.AppStatus = -1;
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

    private async Task VerifyCookiesAsync(CancellationToken cancellationToken)
    {
        List<UserModel> users = await _userService.GetUnverifiedUsersAsync(cancellationToken);

        foreach (var user in users)
        {
            var context = ResilienceContextPool.Shared.Get(cancellationToken);
            context.Properties.Set(LittleHeartResilienceKeys.User, user);

            try
            {
                await _verifyCookiesPipeline.ExecuteAsync(async ctx =>
                {
                    HttpResponseMessage responseMessage = await Globals.HttpClient.SendAsync(new HttpRequestMessage
                    {
                        Method = HttpMethod.Get,
                        RequestUri = new Uri("https://api.bilibili.com/x/web-interface/nav"),
                        Headers = { { "Cookie", user.Cookie } }
                    }, ctx.CancellationToken);
                    await Task.Delay(1000, ctx.CancellationToken);

                    JsonNode? response =
                        JsonNode.Parse(await responseMessage.Content.ReadAsStringAsync(ctx.CancellationToken));
                    if (response == null)
                    {
                        throw new LittleHeartException(Reason.NullResponse);
                    }

                    int? code = (int?)response["code"];
                    if (code == 0)
                    {
                        await _userService.MarkCookieValid(user.Uid, cancellationToken);
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
                            .Error("uid {uid} 提供的cookie错误或已过期",
                                user.Uid);
                        throw new LittleHeartException(Reason.CookieExpired);
                    }
                    else
                    {
                        _logger.ForContext("Response", response.ToJsonString(_options))
                            .Error("uid {uid} 验证cookie时出现预料之外的错误",
                                user.Uid);
                        await _userService.MarkCookieError(user.Uid, cancellationToken);
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
                        await _userService.MarkCookieError(user.Uid, cancellationToken);
                        break;
                    case Reason.NullResponse:
                        _logger.Error(ex,
                            "uid {Uid} 验证cookie时出现 NullResponse 异常，polly尝试多次后依旧发生异常",
                            user.Uid);
                        await _userService.MarkCookieError(user.Uid, cancellationToken);
                        break;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.Error(ex,
                    "uid {Uid} 验证cookie时出现 HttpRequestException 异常，polly尝试多次后依旧发生异常",
                    user.Uid);
                await _userService.MarkCookieError(user.Uid, cancellationToken);
                throw new LittleHeartException(Reason.Ban);
            }
            finally
            {
                ResilienceContextPool.Shared.Return(context);
            }
        }
    }

    private async Task SendMessageAsync(List<UserModel> users, CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();

        foreach (var user in users)
        {
            tasks.Add(_userService.SendMessageAsync(user, cancellationToken));
            await Task.Delay(100, cancellationToken);
        }

        while (tasks.Count != 0)
        {
            Task finishedTask = await Task.WhenAny(tasks);
            if (finishedTask.Exception != null)
            {
                throw finishedTask.Exception;
            }

            tasks.Remove(finishedTask);
        }
    }

    private async Task WatchLiveAsync(List<UserModel> users, CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();
        foreach (var user in users)
        {
            tasks.Add(_userService.WatchLiveAsync(user, cancellationToken));
            await Task.Delay(2000, cancellationToken);
        }

        while (tasks.Count != 0)
        {
            Task finishedTask = await Task.WhenAny(tasks);
            if (finishedTask.Exception != null)
            {
                throw finishedTask.Exception;
            }

            tasks.Remove(finishedTask);
        }
    }
}