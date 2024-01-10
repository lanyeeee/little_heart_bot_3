using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using little_heart_bot_3.Data;
using little_heart_bot_3.Data.Models;
using little_heart_bot_3.Others;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;

namespace little_heart_bot_3.Services.Implements;

public class TargetService : ITargetService
{
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _options;
    private readonly HttpClient _httpClient;
    private readonly IDbContextFactory<LittleHeartDbContext> _factory;

    private readonly ResiliencePipeline _postEPipeline;
    private readonly ResiliencePipeline _postXPipeline;
    private readonly ResiliencePipeline _getExpPipeline;

    public TargetService(
        ILogger logger,
        JsonSerializerOptions options,
        HttpClient httpClient,
        IDbContextFactory<LittleHeartDbContext> factory)
    {
        _logger = logger;
        _options = options;
        _httpClient = httpClient;
        _factory = factory;


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
                    _logger.LogWarning(args.Outcome.Exception,
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
                    _logger.LogWarning(args.Outcome.Exception,
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
                    _logger.LogWarning(args.Outcome.Exception,
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

    public async Task StartAsync(TargetModel target, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(CancellationToken.None);
        db.Targets.Attach(target);

        int? exp = await GetExpAsync(target, cancellationToken);
        if (exp is null)
        {
            //没有粉丝牌或出现了意料之外的错误，直接标记为已完成
            target.Completed = true;
            await db.SaveChangesAsync(CancellationToken.None);
            return;
        }

        target.Exp = exp.Value;
        await db.SaveChangesAsync(CancellationToken.None);

#if DEBUG
        Console.WriteLine($"uid {target.Uid} 在 {target.TargetName} 直播间的经验为 {target.Exp}");
#endif
        _logger.LogTrace("uid {Uid} 在 {TargetName} 直播间的经验为 {Exp}",
            target.Uid,
            target.TargetName,
            target.Exp);

        //如果已经完成，直接返回
        if (IsCompleted(target))
        {
            _logger.LogInformation("uid {Uid} 在 {TargetName} 的任务完成，观看时长 {WatchedSeconds} 秒，获得经验 {Exp}",
                target.Uid,
                target.TargetName,
                target.WatchedSeconds,
                target.Exp);

            target.Completed = true;
            await db.SaveChangesAsync(CancellationToken.None);
            return;
        }

        //否则开始观看直播
        Dictionary<string, string>? ePayload = await GetEPayloadAsync(target, cancellationToken);
        if (ePayload is null)
        {
            return;
        }

        JsonNode? heartbeatData = await PostEAsync(target, ePayload, cancellationToken);
        if (heartbeatData is null)
        {
            return;
        }

        JsonNode id = JsonNode.Parse(ePayload["id"])!;
        if ((int)id[0]! == 0 || (int)id[1]! == 0)
        {
            _logger.LogWarning("uid {Uid} 在 {TargetName} 的任务完成，观看时长为0因为 {TargetName} 的直播间没有选择分区，无法观看",
                target.Uid,
                target.TargetName,
                target.TargetName);

            target.Completed = true;
            await db.SaveChangesAsync(CancellationToken.None);
            return;
        }

        await HeartBeatAsync(target, ePayload, heartbeatData, cancellationToken);
    }

    private async Task<Dictionary<string, string>?> GetEPayloadAsync(TargetModel target,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(CancellationToken.None);
        db.Attach(target);

        var uri = new Uri(
            $"https://api.live.bilibili.com/xlive/web-room/v1/index/getInfoByRoom?&room_id={target.RoomId}");

        HttpResponseMessage responseMessage = await _httpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = uri
        }, cancellationToken);
        JsonNode response = await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, cancellationToken) ??
                            throw new LittleHeartException(Reason.NullResponse);

        int code = (int)response["code"]!;

        if (code == 19002005) //房间已加密
        {
            _logger.LogWithResponse(
                () => _logger
                    .LogWarning("获取uid {uid} 直播间的信息失败",
                        target.TargetUid),
                response.ToJsonString(_options));

            target.Completed = true;
            await db.SaveChangesAsync(CancellationToken.None);
            return null;
        }
        else if (code != 0)
        {
            _logger.LogWithResponse(
                () => _logger
                    .LogError("获取uid {uid} 直播间的信息失败",
                        target.TargetUid),
                response.ToJsonString(_options));
            throw new LittleHeartException(Reason.Ban);
        }

        int parentAreaId = (int)response["data"]!["room_info"]!["parent_area_id"]!;
        int areaId = (int)response["data"]!["room_info"]!["area_id"]!;
        var id = new JsonArray { parentAreaId, areaId, 0, target.RoomId };

        return new Dictionary<string, string>
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
            { "csrf_token", target.UserModel.Csrf },
            { "csrf", target.UserModel.Csrf },
            { "visit_id", "" }
        };
    }

    private async Task<JsonNode?> PostEAsync(
        TargetModel target,
        Dictionary<string, string> ePayload,
        CancellationToken cancellationToken = default)
    {
        ResilienceContext context = ResilienceContextPool.Shared.Get(cancellationToken);
        context.Properties.Set(LittleHeartResilienceKeys.Target, target);

        try
        {
            return await _postEPipeline.ExecuteAsync(async ctx =>
            {
                HttpResponseMessage responseMessage = await _httpClient.SendAsync(new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri("https://live-trace.bilibili.com/xlive/data-interface/v1/x25Kn/E"),
                    Headers = { { "Cookie", target.UserModel.Cookie } },
                    Content = new FormUrlEncodedContent(ePayload)
                }, ctx.CancellationToken);

                JsonNode response =
                    await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, ctx.CancellationToken) ??
                    throw new LittleHeartException(Reason.NullResponse);

                int code = (int)response["code"]!;
                if (code != 0)
                {
                    _logger.LogWithResponse(
                        () => _logger
                            .LogError("uid {uid} 发送E心跳包失败",
                                target.TargetUid),
                        response.ToJsonString(_options));
                    throw new LittleHeartException(Reason.Ban);
                }

                return response["data"]!;
            }, context);
        }
        catch (LittleHeartException ex)
        {
            switch (ex.Reason)
            {
                case Reason.NullResponse:
                    _logger.LogError("uid {Uid} 给 {TargetName} 发送E心跳包时出现 NullResponse 异常，且重试多次后仍然出现异常",
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
            _logger.LogError(ex,
                "uid {Uid} 给 {TargetName} 发送E心跳包时出现 HttpRequestException 异常，且重试多次后仍然出现异常",
                target.Uid,
                target.TargetName);
            throw new LittleHeartException(Reason.Ban);
        }
        catch (TaskCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "uid {Uid} 给 {TargetName} 发送E心跳包时发生意料之外的异常",
                target.TargetUid,
                target.TargetName);
            return null;
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }

    private async Task<JsonNode?> PostXAsync(
        TargetModel target,
        Dictionary<string, string> payload,
        CancellationToken cancellationToken = default)
    {
        var context = ResilienceContextPool.Shared.Get(cancellationToken);
        context.Properties.Set(LittleHeartResilienceKeys.Target, target);
        try
        {
            return await _postXPipeline.ExecuteAsync(async ctx =>
            {
                HttpResponseMessage responseMessage = await _httpClient.SendAsync(new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri("https://live-trace.bilibili.com/xlive/data-interface/v1/x25Kn/X"),
                    Headers = { { "Cookie", target.UserModel.Cookie } },
                    Content = new FormUrlEncodedContent(payload)
                }, ctx.CancellationToken);
                JsonNode response =
                    await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, ctx.CancellationToken) ??
                    throw new LittleHeartException(Reason.NullResponse);

                int code = (int)response["code"]!;
                if (code == -504)
                {
                    _logger.LogWithResponse(
                        () => _logger.LogWarning("uid {Uid} 给 {TargetName} 发送X心跳包失败，服务器调用超时",
                            target.Uid,
                            target.TargetName),
                        response.ToJsonString(_options));
                    throw new LittleHeartException(Reason.ServerTimeout);
                }
                else if (code == 1012001)
                {
                    //签名错误，心跳包加密失败
                    Console.WriteLine(payload["s"]);
                    _logger.LogWithResponse(
                        () => _logger.LogError("uid {Uid} 给 {TargetName} 发送X心跳包失败，因为签名错误，心跳包加密失败",
                            target.TargetUid,
                            target.TargetName),
                        response.ToJsonString(_options));
                    return null;
                }
                else if (code == 1012002)
                {
                    //没有按照规定的时间间隔发送心跳包
                    _logger.LogWithResponse(
                        () => _logger.LogWarning(
                            "uid {Uid} 给 {TargetName} 发送X心跳包失败，因为没有按照规定的时间间隔发送心跳包",
                            target.TargetUid,
                            target.TargetName),
                        response.ToJsonString(_options));
                    return null;
                }
                else if (code == 1012003)
                {
                    //心跳包时间戳错误
                    _logger.LogWithResponse(
                        () => _logger.LogWarning("uid {Uid} 给 {TargetName} 发送X心跳包失败，时间戳错误",
                            target.Uid,
                            target.TargetName),
                        response.ToJsonString(_options));
                    return null;
                }


                if (code != 0)
                {
                    _logger.LogWithResponse(
                        () => _logger.LogError("uid {Uid} 给 {TargetName} 发送X心跳包失败",
                            target.TargetUid,
                            target.TargetName),
                        response.ToJsonString(_options));
                    throw new LittleHeartException(Reason.Ban);
                }

                return response["data"];
            }, context);
        }
        catch (LittleHeartException ex)
        {
            switch (ex.Reason)
            {
                case Reason.NullResponse:
                    _logger.LogError("uid {Uid} 给 {TargetName} 发送X心跳包时出现 NullResponse 异常，且重试多次后仍然出现异常",
                        target.Uid,
                        target.TargetName);
                    ex.Reason = Reason.Ban;
                    throw;
                case Reason.ServerTimeout:
                    _logger.LogError("uid {Uid} 给 {TargetName} 发送X心跳包时出现 ServerTimeout 异常，且重试多次后仍然出现异常",
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
            _logger.LogError(ex,
                "uid {Uid} 给 {TargetName} 发送X心跳包时出现 HttpRequestException 异常，且重试多次后仍然出现异常",
                target.Uid,
                target.TargetName);
            throw new LittleHeartException(Reason.Ban);
        }
        catch (TaskCanceledException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "uid {Uid} 给 {TargetName} 发送X心跳包时发生 Json 异常",
                target.TargetUid,
                target.TargetName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "uid {Uid} 给 {TargetName} 发送X心跳包时发生意料之外的异常",
                target.TargetUid,
                target.TargetName);
            return null;
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }

    private async Task<int?> GetExpAsync(TargetModel target, CancellationToken cancellationToken = default)
    {
        var uri = new Uri(
            $"https://api.live.bilibili.com/xlive/app-ucenter/v1/fansMedal/fans_medal_info?target_id={target.TargetUid}");

        var context = ResilienceContextPool.Shared.Get(cancellationToken);
        context.Properties.Set(LittleHeartResilienceKeys.Target, target);
        try
        {
            return await _getExpPipeline.ExecuteAsync(async ctx =>
            {
                HttpResponseMessage responseMessage = await _httpClient.SendAsync(new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = uri,
                    Headers = { { "cookie", target.UserModel.Cookie } }
                }, ctx.CancellationToken);

                JsonNode response =
                    await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, ctx.CancellationToken) ??
                    throw new LittleHeartException(Reason.NullResponse);

                int code = (int)response["code"]!;
                if (code == -101)
                {
                    _logger.LogWithResponse(
                        () => _logger.LogWarning("uid {Uid} 的cookie已过期",
                            target.Uid),
                        response.ToJsonString(_options));
                    throw new LittleHeartException(Reason.CookieExpired);
                }

                if (code != 0)
                {
                    _logger.LogWithResponse(
                        () => _logger.LogError("uid {Uid} 查询 {TargetName} 的粉丝牌信息失败",
                            target.Uid,
                            target.TargetName),
                        response.ToJsonString(_options));
                    throw new LittleHeartException(Reason.Ban);
                }

                bool hasMedal = (bool)response["data"]!["has_fans_medal"]!;
                if (!hasMedal)
                {
                    _logger.LogWithResponse(
                        () => _logger.LogWarning("uid {uid} 未持有 {TargetName} 的粉丝牌",
                            target.Uid,
                            target.TargetName),
                        response.ToJsonString(_options));
                    throw new LittleHeartException(Reason.WithoutMedal);
                }

                int? exp = (int?)response["data"]!["my_fans_medal"]!["today_feed"];

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
                    _logger.LogError("uid {Uid} 查询 {TargetName} 的粉丝牌信息时出现 NullResponse 异常，且重试多次后仍然出现异常",
                        target.Uid,
                        target.TargetName);
                    ex.Reason = Reason.Ban;
                    throw;
                case Reason.WithoutMedal:
                {
                    await using var db = await _factory.CreateDbContextAsync(CancellationToken.None);
                    db.Remove(target);
                    await db.SaveChangesAsync(CancellationToken.None);
                    return null;
                }
                default:
                    return null;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "uid {Uid} 查询 {TargetName} 的粉丝牌信息时出现 HttpRequestException 异常，且重试多次后仍然出现异常",
                target.Uid,
                target.TargetName);
            throw new LittleHeartException(Reason.Ban);
        }
        catch (TaskCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "uid {Uid} 获取 {TargetName} 粉丝牌经验时发生意料之外的异常",
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

    private async Task HeartBeatAsync(
        TargetModel target,
        Dictionary<string, string> ePayload,
        JsonNode heartbeatData,
        CancellationToken cancellationToken = default)
    {
        while (true)
        {
            int interval = (int)heartbeatData["heartbeat_interval"]!;
            await Task.Delay(interval * 1000, cancellationToken);

            var xPayload = GetXPayload(ePayload, heartbeatData);

            var data = await PostXAsync(target, xPayload, cancellationToken);
            if (data is null)
            {
                _logger.LogTrace("因为 uid {Uid} 给 {TargetName} 发送X心跳包失败，停止继续发包，当前观看时长 {WatchedSeconds} 秒",
                    target.Uid,
                    target.TargetName,
                    target.WatchedSeconds);
                return;
            }

            heartbeatData = data;

            interval = (int)heartbeatData["heartbeat_interval"]!;
            await using var db = await _factory.CreateDbContextAsync(CancellationToken.None);
            db.Attach(target);
            target.WatchedSeconds += interval;
            await db.SaveChangesAsync(CancellationToken.None);

            _logger.LogTrace("uid {Uid} 给 {TargetName} 发送X心跳包成功，当前观看时长 {WatchedSeconds} 秒",
                target.Uid,
                target.TargetName,
                target.WatchedSeconds);

            //每隔5分钟检查一次是否完成
            if (target.WatchedSeconds % 300 == 0)
            {
                //先获取经验
                int? exp = await GetExpAsync(target, cancellationToken);
                if (exp is null)
                {
                    //出现了意料之外的错误，直接标记为已完成
                    target.Completed = true;
                    await db.SaveChangesAsync(CancellationToken.None);
                    return;
                }

                target.Exp = exp.Value;
                await db.SaveChangesAsync(CancellationToken.None);
                //再根据经验和观看时长判断是否完成
                if (IsCompleted(target))
                {
                    _logger.LogInformation("uid {Uid} 在 {TargetName} 的任务完成，观看时长 {WatchedSeconds} 秒，获得经验 {Exp}",
                        target.Uid,
                        target.TargetName,
                        target.WatchedSeconds,
                        target.Exp);
                    target.Completed = true;
                    await db.SaveChangesAsync(CancellationToken.None);
                    return;
                }
            }
        }
    }

    private Dictionary<string, string> GetXPayload(
        Dictionary<string, string> ePayload,
        JsonNode heartbeatData)
    {
        long ts = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        var id = JsonNode.Parse(ePayload["id"])!;
        var device = JsonNode.Parse(ePayload["device"])!;

        string key = (string)heartbeatData["secret_key"]!;
        int[] rules = heartbeatData["secret_rule"]!.Deserialize<int[]>()!;
        string data = JsonSerializer.Serialize(new
        {
            platform = "web",
            parent_id = id[0],
            area_id = id[1],
            seq_id = id[2],
            room_id = id[3],
            buvid = device[0],
            uuid = device[1],
            ets = heartbeatData["timestamp"],
            time = heartbeatData["heartbeat_interval"],
            ts
        });

        return new Dictionary<string, string>
        {
            ["s"] = LiveHeartbeatEncryptor.Encrypt(data, rules, key),
            ["id"] = ePayload["id"],
            ["device"] = ePayload["device"],
            ["ets"] = heartbeatData["timestamp"]!.GetValue<long>().ToString(),
            ["benchmark"] = key,
            ["time"] = heartbeatData["heartbeat_interval"]!.GetValue<long>().ToString(),
            ["ts"] = ts.ToString(),
            ["ua"] = ePayload["ua"],
            ["csrf_token"] = ePayload["csrf"],
            ["csrf"] = ePayload["csrf"],
            ["visit_id"] = ""
        };
    }
}