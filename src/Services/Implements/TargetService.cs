using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using little_heart_bot_3.Models;
using little_heart_bot_3.Others;
using little_heart_bot_3.Repositories;
using Polly;
using Polly.Retry;
using Serilog.Core;

namespace little_heart_bot_3.Services.Implements;

public partial class TargetService : ITargetService
{
    private readonly Logger _logger;
    private readonly ITargetRepository _targetRepository;

    private readonly JsonSerializerOptions _options;

    private readonly ResiliencePipeline _postEPipeline;
    private readonly ResiliencePipeline _postXPipeline;
    private readonly ResiliencePipeline _getExpPipeline;

    public TargetService(Logger logger, ITargetRepository targetRepository)
    {
        _logger = logger;
        _targetRepository = targetRepository;

        _options = Globals.JsonSerializerOptions;

        _postEPipeline = new ResiliencePipelineBuilder()
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
                    var target = args.Context.Properties.GetValue(LittleHeartResilienceKeys.Target, null)!;
                    _logger.Warning(args.Outcome.Exception,
                        "uid {Uid} 发送E心跳包时遇到异常，准备在 {RetryDelay} 秒后进行第 {AttemptNumber} 次重试",
                        target.Uid,
                        args.RetryDelay.TotalSeconds,
                        args.AttemptNumber);
                    return default;
                }
            })
            .Build();

        _postXPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<LittleHeartException>(ex =>
                        ex.Reason is Reason.NullResponse or Reason.ServerTimeout)
                    .Handle<HttpRequestException>(),
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Constant,
                MaxRetryAttempts = 5,
                OnRetry = args =>
                {
                    var target = args.Context.Properties.GetValue(LittleHeartResilienceKeys.Target, null)!;
                    _logger.Warning(args.Outcome.Exception,
                        "uid {Uid} 发送X心跳包时遇到异常，准备在 {RetryDelay} 秒后进行第 {AttemptNumber} 次重试",
                        target.Uid,
                        args.RetryDelay.TotalSeconds,
                        args.AttemptNumber);
                    return default;
                }
            })
            .Build();

        _getExpPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<LittleHeartException>(ex =>
                        ex.Reason == Reason.NullResponse)
                    .Handle<HttpRequestException>(),
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                MaxRetryAttempts = 5,
                OnRetry = args =>
                {
                    var target = args.Context.Properties.GetValue(LittleHeartResilienceKeys.Target, null)!;
                    _logger.Warning(args.Outcome.Exception,
                        "uid {Uid} 查询 {TargetName} 粉丝牌信息时遇到异常，准备在 {RetryDelay} 秒后进行第 {AttemptNumber} 次重试",
                        target.Uid,
                        target.TargetName,
                        args.RetryDelay.TotalSeconds,
                        args.AttemptNumber);
                    return default;
                }
            })
            .Build();
    }

    public async Task StartAsync(TargetModel target, string? cookie, string? csrf,
        CancellationToken cancellationToken = default)
    {
        int? exp = await GetExpAsync(target, cookie, cancellationToken);
        if (exp == null)
        {
            //没有粉丝牌或出现了意料之外的错误，直接标记为已完成
            target.Completed = 1;
            await SetCompletedAsync(target.Completed, target.Id, cancellationToken);
            return;
        }

        target.Exp = exp.Value;
        await SetExpAsync(target.Exp, target.Id, cancellationToken);
#if DEBUG
        Console.WriteLine($"uid {target.Uid} 在 {target.TargetName} 直播间的经验为 {target.Exp}");
#endif
        _logger.Verbose("uid {Uid} 在 {TargetName} 直播间的经验为 {Exp}",
            target.Uid,
            target.TargetName,
            target.Exp);

        //如果已经完成，直接返回
        if (IsCompleted(target))
        {
            _logger.Information("uid {Uid} 在 {TargetName} 的任务完成，观看时长 {WatchedSeconds} 秒，获得经验 {Exp}",
                target.Uid,
                target.TargetName,
                target.WatchedSeconds,
                target.Exp);
            target.Completed = 1;
            await SetCompletedAsync(target.Completed, target.Id, cancellationToken);
            return;
        }

        //否则开始观看直播
        Dictionary<string, string?>? payload = await PostEAsync(target, cookie, csrf, cancellationToken);
        if (payload == null)
        {
            return;
        }

        JsonNode id = JsonNode.Parse(payload["id"]!)!;
        if ((int?)id[0] == 0 || (int?)id[1] == 0)
        {
            _logger.Warning("uid {Uid} 在 {TargetName} 的任务完成，观看时长为0因为 {TargetName} 的直播间没有选择分区，无法观看",
                target.Uid,
                target.TargetName,
                target.TargetName);
            target.Completed = 1;
            await SetCompletedAsync(target.Completed, target.Id, cancellationToken);
            return;
        }

        await HeartBeatAsync(target, cookie, payload, cancellationToken);
    }

    private async Task<Dictionary<string, string?>?> GetPayloadAsync(TargetModel target, string? csrf,
        CancellationToken cancellationToken = default)
    {
        var uri = new Uri(
            $"https://api.live.bilibili.com/xlive/web-room/v1/index/getInfoByRoom?&room_id={target.RoomId}");

        HttpResponseMessage responseMessage = await Globals.HttpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = uri
        }, cancellationToken);
        JsonNode? response = JsonNode.Parse(await responseMessage.Content.ReadAsStringAsync(cancellationToken));
        if (response == null)
        {
            throw new LittleHeartException(Reason.NullResponse);
        }

        int? code = (int?)response["code"];

        if (code == 19002005) //房间已加密
        {
            _logger.ForContext("Response", response.ToJsonString(_options))
                .Warning("获取uid {uid} 直播间的信息失败",
                    target.TargetUid);
            await SetCompletedAsync(1, target.Id, cancellationToken);
            return null;
        }
        else if (code != 0)
        {
            _logger.ForContext("Response", response.ToJsonString(_options))
                .Error("获取uid {uid} 直播间的信息失败",
                    target.TargetUid);
            throw new LittleHeartException(Reason.Ban);
        }

        int? parentAreaId = (int?)response["data"]!["room_info"]!["parent_area_id"];
        int? areaId = (int?)response["data"]!["room_info"]!["area_id"];
        var id = new JsonArray { parentAreaId, areaId, 0, int.Parse(target.RoomId!) };

        return new Dictionary<string, string?>
        {
            { "id", id.ToJsonString(_options) },
            { "device", "[\"AUTO8716422349901853\",\"3E739D10D-174A-10DD5-61028-A5E3625BE56450692infoc\"]" },
            { "ts", DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString() },
            { "is_patch", "0" },
            { "heart_beat", "[]" },
            {
                "ua",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/80.0.3987.163 Safari/537.36"
            },
            { "csrf_token", csrf },
            { "csrf", csrf },
            { "visit_id", "" }
        };
    }

    private async Task<Dictionary<string, string?>?> PostEAsync(TargetModel target, string? cookie, string? csrf,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, string?>? payload = await GetPayloadAsync(target, csrf, cancellationToken);
        if (payload == null)
        {
            return null;
        }

        ResilienceContext context = ResilienceContextPool.Shared.Get(cancellationToken);
        context.Properties.Set(LittleHeartResilienceKeys.Target, target);

        try
        {
            await _postEPipeline.ExecuteAsync(async ctx =>
            {
                HttpResponseMessage responseMessage = await Globals.HttpClient.SendAsync(new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri("https://live-trace.bilibili.com/xlive/data-interface/v1/x25Kn/E"),
                    Headers = { { "Cookie", cookie } },
                    Content = new FormUrlEncodedContent(payload)
                }, ctx.CancellationToken);

                JsonNode? response =
                    JsonNode.Parse(await responseMessage.Content.ReadAsStringAsync(ctx.CancellationToken));
                if (response == null)
                {
                    throw new LittleHeartException(Reason.NullResponse);
                }

                int? code = (int?)response["code"];
                if (code != 0)
                {
                    //TODO: 以后需要记录 风控 和 Cookie过期 的code，专门处理
                    _logger.ForContext("Response", response.ToJsonString(_options))
                        .Error("uid {uid} 发送E心跳包失败",
                            target.TargetUid);
                    throw new LittleHeartException(Reason.Ban);
                }

                payload["ets"] = (string?)response["data"]!["timestamp"];
                payload["secret_key"] = (string?)response["data"]!["secret_key"];
                payload["heartbeat_interval"] = (string?)response["data"]!["heartbeat_interval"];
                payload["secret_rule"] = response["data"]!["secret_rule"]!.ToJsonString(_options);
            }, context);
        }
        catch (LittleHeartException ex)
        {
            switch (ex.Reason)
            {
                case Reason.NullResponse:
                    _logger.Error("uid {Uid} 给 {TargetName} 发送E心跳包时出现 NullResponse 异常，且重试多次后仍然出现异常",
                        target.Uid,
                        target.TargetName);
                    ex.Reason = Reason.Ban;
                    throw;
                default:
                    throw;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.Error(ex,
                "uid {Uid} 给 {TargetName} 发送E心跳包时出现 HttpRequestException 异常，且重试多次后仍然出现异常",
                target.Uid,
                target.TargetName);
            throw new LittleHeartException(Reason.Ban);
        }
        catch (Exception ex)
        {
            _logger.Fatal(ex, "uid {Uid} 给 {TargetName} 发送E心跳包时发生意料之外的异常",
                target.TargetUid,
                target.TargetName);
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }

        return payload;
    }

    private async Task<string?> GenerateSAsync(Dictionary<string, string?> payload, string ts,
        CancellationToken cancellationToken = default)
    {
        var t = new JsonObject
        {
            { "id", JsonNode.Parse(payload["id"]!) },
            { "device", payload["device"] },
            { "ets", int.Parse(payload["ets"]!) },
            { "benchmark", payload["secret_key"] },
            { "time", int.Parse(payload["heartbeat_interval"]!) },
            { "ts", long.Parse(ts) },
            { "ua", payload["ua"] }
        };

        var sPayload = new JsonObject
        {
            { "t", t },
            { "r", payload["secret_rule"] }
        };

        HttpResponseMessage responseMessage = await Globals.HttpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri("http://localhost:3000/enc"),
            Content = new StringContent(sPayload.ToJsonString(_options), Encoding.UTF8,
                "application/json")
        }, cancellationToken);
        JsonNode? response = JsonNode.Parse(await responseMessage.Content.ReadAsStringAsync(cancellationToken));
        if (response == null)
        {
            throw new LittleHeartException(Reason.NullResponse);
        }

        return (string?)response["s"];
    }

    private async Task PostXAsync(TargetModel target, string? cookie, Dictionary<string, string?> payload,
        CancellationToken cancellationToken = default)
    {
        string ts = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
        var xPayload = new Dictionary<string, string?>
        {
            { "s", await GenerateSAsync(payload, ts, cancellationToken) },
            { "id", payload["id"] },
            { "device", payload["device"] },
            { "ets", payload["ets"] },
            { "benchmark", payload["secret_key"] },
            { "time", payload["heartbeat_interval"] },
            { "ts", ts },
            { "ua", payload["ua"] },
            { "csrf_token", payload["csrf"] },
            { "csrf", payload["csrf"] },
            { "visit_id", "" }
        };

        var context = ResilienceContextPool.Shared.Get(cancellationToken);
        context.Properties.Set(LittleHeartResilienceKeys.Target, target);

        try
        {
            await _postXPipeline.ExecuteAsync(async ctx =>
            {
                HttpResponseMessage responseMessage = await Globals.HttpClient.SendAsync(new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri("https://live-trace.bilibili.com/xlive/data-interface/v1/x25Kn/X"),
                    Headers = { { "Cookie", cookie } },
                    Content = new FormUrlEncodedContent(xPayload)
                }, ctx.CancellationToken);
                JsonNode? response =
                    JsonNode.Parse(await responseMessage.Content.ReadAsStringAsync(ctx.CancellationToken));
                if (response == null)
                {
                    throw new LittleHeartException(Reason.NullResponse);
                }

                int? code = (int?)response["code"];
                if (code == -504)
                {
#if DEBUG
                    Console.WriteLine($"uid {target.Uid} 给 {target.TargetName} 发送X心跳包失败，服务器调用超时");
#endif
                    _logger.ForContext("Response", response.ToJsonString(_options))
                        .Warning("uid {Uid} 给 {TargetName} 发送X心跳包失败，服务器调用超时",
                            target.Uid,
                            target.TargetName);
                    throw new LittleHeartException(Reason.ServerTimeout);
                }

                if (code == 1012002)
                {
                    //TODO: 已经忘了是什么错误了，以后需要记录，所以即使是预料之内的错误也定级为Error
                    _logger.ForContext("Response", response.ToJsonString(_options))
                        .Error("uid {Uid} 给 {TargetName} 发送X心跳包失败",
                            target.TargetUid,
                            target.TargetName);
                }

                if (code != 0)
                {
                    //TODO: 以后需要记录风控的code，专门处理
                    _logger.ForContext("Response", response.ToJsonString(_options))
                        .Error("uid {Uid} 给 {TargetName} 发送X心跳包失败",
                            target.TargetUid,
                            target.TargetName);
                    throw new LittleHeartException(Reason.Ban);
                }

                payload["ets"] = (string?)response["data"]!["timestamp"];
                payload["secret_key"] = (string?)response["data"]!["secret_key"];
                payload["heartbeat_interval"] = (string?)response["data"]!["heartbeat_interval"];
                JsonArray id = JsonNode.Parse(payload["id"]!)!.AsArray();
                id[2] = (int?)id[2] + 1;
                payload["id"] = id.ToJsonString(_options);
            }, context);
        }
        catch (LittleHeartException ex)
        {
            switch (ex.Reason)
            {
                case Reason.NullResponse:
                    _logger.Error("uid {Uid} 给 {TargetName} 发送X心跳包时出现 NullResponse 异常，且重试多次后仍然出现异常",
                        target.Uid,
                        target.TargetName);
                    ex.Reason = Reason.Ban;
                    throw;
                case Reason.ServerTimeout:
                    _logger.Error("uid {Uid} 给 {TargetName} 发送X心跳包时出现 ServerTimeout 异常，且重试多次后仍然出现异常",
                        target.Uid,
                        target.TargetName);
                    ex.Reason = Reason.Ban;
                    throw;
                default:
                    throw;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.Error(ex,
                "uid {Uid} 给 {TargetName} 发送X心跳包时出现 HttpRequestException 异常，且重试多次后仍然出现异常",
                target.Uid,
                target.TargetName);
            throw new LittleHeartException(Reason.Ban);
        }
        catch (Exception ex)
        {
            _logger.Fatal(ex, "uid {Uid} 给 {TargetName} 发送X心跳包时发生意料之外的异常",
                target.TargetUid,
                target.TargetName);
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }

    private async Task<int?> GetExpAsync(TargetModel target, string? cookie,
        CancellationToken cancellationToken = default)
    {
        var uri = new Uri(
            $"https://api.live.bilibili.com/xlive/app-ucenter/v1/fansMedal/fans_medal_info?target_id={target.TargetUid}");

        var context = ResilienceContextPool.Shared.Get(cancellationToken);
        context.Properties.Set(LittleHeartResilienceKeys.Target, target);
        try
        {
            return await _getExpPipeline.ExecuteAsync(async ctx =>
            {
                HttpResponseMessage responseMessage = await Globals.HttpClient.SendAsync(new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = uri,
                    Headers = { { "cookie", cookie } }
                }, ctx.CancellationToken);

                JsonNode? response =
                    JsonNode.Parse(await responseMessage.Content.ReadAsStringAsync(ctx.CancellationToken));
                if (response == null)
                {
                    throw new LittleHeartException(Reason.NullResponse);
                }

                int code = (int)response["code"]!;
                if (code == -101)
                {
                    _logger.ForContext("Response", response.ToJsonString(_options))
                        .Warning("uid {Uid} 的cookie已过期",
                            target.Uid);
                    throw new LittleHeartException(Reason.CookieExpired);
                }

                if (code != 0)
                {
                    //TODO: 以后需要记录风控的code，专门处理
                    _logger.ForContext("Response", response.ToJsonString(_options))
                        .Error("uid {Uid} 查询 {TargetName} 的粉丝牌信息失败",
                            target.Uid,
                            target.TargetName);
                    throw new LittleHeartException(Reason.Ban);
                }

                bool hasMedal = (bool)response["data"]!["has_fans_medal"]!;
                if (!hasMedal)
                {
                    _logger.ForContext("Response", response.ToJsonString(_options))
                        .Warning("uid {uid} 未持有 {TargetName} 的粉丝牌",
                            target.Uid,
                            target.TargetName);
                    throw new LittleHeartException(Reason.WithoutMedal);
                }

                int exp = response["data"]!["intimacy_tasks"]!.AsArray()
                    .Where(task =>
                    {
                        string? title = (string?)task!["title"];
                        return title is "观看直播" or "每日首条弹幕" or "每日首次给主播双击点赞";
                    })
                    .Select(task => (int)task!["cur_progress"]!)
                    .Sum();

                return exp;
            }, context);
        }
        catch (LittleHeartException ex)
        {
            switch (ex.Reason)
            {
                case Reason.Ban:
                case Reason.CookieExpired:
                    throw;
                case Reason.NullResponse:
                    _logger.Error("uid {Uid} 查询 {TargetName} 的粉丝牌信息时出现 NullResponse 异常，且重试多次后仍然出现异常",
                        target.Uid,
                        target.TargetName);
                    ex.Reason = Reason.Ban;
                    throw;
                case Reason.WithoutMedal:
                    await DeleteAsync(target.Id, cancellationToken);
                    return null;
                default:
                    return null;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.Error(ex,
                "uid {Uid} 查询 {TargetName} 的粉丝牌信息时出现 HttpRequestException 异常，且重试多次后仍然出现异常",
                target.Uid,
                target.TargetName);
            throw new LittleHeartException(Reason.Ban);
        }
        catch (Exception ex)
        {
            _logger.Fatal(ex, "uid {Uid} 获取 {TargetName} 粉丝牌经验时发生意料之外的异常",
                target.TargetUid,
                target.TargetName);
            return null;
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }

    private bool IsCompleted(TargetModel target)
    {
        //只有经验>=1500或观看时长>=75分钟才判定为完成
        return target is not { Exp: < 1500, WatchedSeconds: < 75 * 60 };
    }

    private async Task HeartBeatAsync(TargetModel target, string? cookie, Dictionary<string, string?> payload,
        CancellationToken cancellationToken = default)
    {
        int interval = int.Parse(payload["heartbeat_interval"]!);

        await Task.Delay(interval * 1000, cancellationToken);

        while (true)
        {
            await PostXAsync(target, cookie, payload, cancellationToken);

            interval = int.Parse(payload["heartbeat_interval"]!);
            target.WatchedSeconds += interval;
            await SetWatchedSecondsAsync(target.WatchedSeconds, target.Id, cancellationToken);

            _logger.Verbose("uid {Uid} 给 {TargetName} 发送X心跳包成功，当前观看时长 {WatchedSeconds} 秒",
                target.Uid,
                target.TargetName,
                target.WatchedSeconds);

            //每隔5分钟检查一次是否完成，完成则返回
            if (target.WatchedSeconds % 300 == 0 && IsCompleted(target))
            {
                _logger.Information("uid {Uid} 在 {TargetName} 的任务完成，观看时长 {WatchedSeconds} 秒，获得经验 {Exp}",
                    target.Uid,
                    target.TargetName,
                    target.WatchedSeconds,
                    target.Exp);
                target.Completed = 1;
                await SetCompletedAsync(target.Completed, target.Id, cancellationToken);
                return;
            }

            await Task.Delay(interval * 1000, cancellationToken);
        }
    }
}