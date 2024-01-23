using System.Text.Json;
using System.Text.Json.Nodes;
using little_heart_bot_3.Crypto;
using little_heart_bot_3.Data;
using little_heart_bot_3.Data.Models;
using little_heart_bot_3.Others;
using Microsoft.EntityFrameworkCore;

namespace little_heart_bot_3.Services.Implements;

public abstract class TargetService : ITargetService
{
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _options;
    private readonly IApiService _apiService;
    private readonly IDbContextFactory<LittleHeartDbContext> _dbContextFactory;


    protected TargetService(ILogger logger,
        JsonSerializerOptions options,
        IApiService apiService,
        IDbContextFactory<LittleHeartDbContext> dbContextFactory)
    {
        _logger = logger;
        _options = options;
        _apiService = apiService;
        _dbContextFactory = dbContextFactory;
    }

    public async Task StartAsync(TargetModel target, CancellationToken cancellationToken = default)
    {
        try
        {
            int? exp = await GetExpAsync(target, cancellationToken);
            if (exp is null)
            {
                return;
            }

            target.Exp = exp.Value;

            _logger.LogDebug("uid {Uid} 在 {TargetUid}({TargetName}) 直播间的经验为 {Exp}",
                target.Uid,
                target.TargetUid,
                target.TargetName,
                target.Exp);

            //如果已经完成，直接返回
            if (IsCompleted(target))
            {
                _logger.LogInformation("uid {Uid} 在 {TargetUid}({TargetName}) 的任务完成，观看时长 {WatchedSeconds} 秒，获得经验 {Exp}",
                    target.Uid,
                    target.TargetUid,
                    target.TargetName,
                    target.WatchedSeconds,
                    target.Exp);

                return;
            }

            //否则开始观看直播
            _logger.LogInformation("uid {Uid} 开始观看 {TargetUid}({TargetName}) 的直播",
                target.Uid,
                target.TargetUid,
                target.TargetName);
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
                _logger.LogInformation("uid {Uid} 在 {TargetUid}({TargetName}) 的任务完成，观看时长为0因为该直播间没有选择分区，无法观看",
                    target.Uid,
                    target.TargetUid,
                    target.TargetName);

                return;
            }

            await HeartBeatAsync(target, ePayload, heartbeatData, cancellationToken);
        }
        catch (LittleHeartException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            _logger.LogCritical("uid {Uid} 在 {TargetUid}({TargetName}) 的任务发生意料之外的异常",
                target.Uid,
                target.TargetUid,
                target.TargetName);
        }
        finally
        {
            target.Completed = true;
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            db.Targets.Update(target);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="target"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="LittleHeartException">
    /// <br/>Reason.RiskControl
    /// </exception>
    private async Task<Dictionary<string, string>?> GetEPayloadAsync(
        TargetModel target,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiService.GetEPayloadAsync(target, cancellationToken);

            int code = (int)response["code"]!;
            switch (code)
            {
                case 0:
                    int parentAreaId = (int)response["data"]!["room_info"]!["parent_area_id"]!;
                    int areaId = (int)response["data"]!["room_info"]!["area_id"]!;
                    var id = new JsonArray { parentAreaId, areaId, 0, target.RoomId };

                    return new Dictionary<string, string>
                    {
                        { "id", id.ToJsonString(_options) },
                        {
                            "device", "[\"AUTO8716422349901853\",\"3E739D10D-174A-10DD5-61028-A5E3625BE56450692infoc\"]"
                        },
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
                case 19002005: //房间已加密
                    _logger.LogWithResponse(
                        () => _logger
                            .LogWarning("uid {Uid} 获取 {TargetUid}({TargetName}) 直播间的信息失败，因为房间已加密",
                                target.Uid,
                                target.TargetUid,
                                target.TargetName),
                        response.ToJsonString(_options));
                    return null;
                default:
                    _logger.LogWithResponse(
                        () => _logger
                            .LogError("uid {Uid} 获取 {TargetUid}({TargetName}) 直播间的信息失败",
                                target.Uid,
                                target.TargetUid,
                                target.TargetName),
                        response.ToJsonString(_options));
                    throw new LittleHeartException(Reason.RiskControl);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "uid {Uid} 获取 {TargetUid}({TargetName}) 的E心跳包Payload时出现 HttpRequestException 异常，且重试多次后仍然出现异常",
                target.Uid,
                target.TargetUid,
                target.TargetName);
            throw new LittleHeartException(ex.Message, ex, Reason.RiskControl);
        }
        catch (LittleHeartException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "uid {Uid} 获取 {TargetUid}({TargetName}) 的E心跳包Payload时发生意料之外的异常",
                target.Uid,
                target.TargetUid,
                target.TargetName);
            return null;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="target"></param>
    /// <param name="ePayload"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>payload</returns>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="LittleHeartException">
    /// <br/>Reason.RiskControl
    /// </exception>
    private async Task<JsonNode?> PostEAsync(
        TargetModel target,
        Dictionary<string, string> ePayload,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiService.PostEAsync(target, ePayload, cancellationToken);

            int code = (int)response["code"]!;
            switch (code)
            {
                case 0:
                    return response["data"];
                default:
                    _logger.LogWithResponse(
                        () => _logger
                            .LogError("uid {Uid} 给 {TargetUid}({TargetName}) 发送E心跳包失败",
                                target.Uid,
                                target.TargetUid,
                                target.TargetName),
                        response.ToJsonString(_options));
                    throw new LittleHeartException(Reason.RiskControl);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "uid {Uid} 给 {TargetUid}({TargetName}) 发送E心跳包时出现 HttpRequestException 异常，且重试多次后仍然出现异常",
                target.Uid,
                target.TargetUid,
                target.TargetName);
            throw new LittleHeartException(ex.Message, ex, Reason.RiskControl);
        }
        catch (LittleHeartException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "uid {Uid} 给 {TargetUid}({TargetName}) 发送E心跳包时发生意料之外的异常",
                target.Uid,
                target.TargetUid,
                target.TargetName);
            return null;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="target"></param>
    /// <param name="payload"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>payload</returns>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="LittleHeartException">
    /// <br/>Reason.RiskControl
    /// </exception>
    private async Task<JsonNode?> PostXAsync(
        TargetModel target,
        Dictionary<string, string> payload,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiService.PostXAsync(target, payload, cancellationToken);

            int code = (int)response["code"]!;
            switch (code)
            {
                case 0:
                    return response["data"];
                case -504:
                    _logger.LogWithResponse(
                        () => _logger.LogWarning("uid {Uid} 给 {TargetUid}({TargetName}) 发送X心跳包失败，服务器调用超时",
                            target.Uid,
                            target.TargetUid,
                            target.TargetName),
                        response.ToJsonString(_options));
                    throw new HttpRequestException(response["message"]!.ToString());
                case 1012001:
                    //签名错误，心跳包加密失败
                    _logger.LogWithResponse(
                        () => _logger.LogError("uid {Uid} 给 {TargetUid}({TargetName}) 发送X心跳包失败，因为签名错误，心跳包加密失败",
                            target.Uid,
                            target.TargetUid,
                            target.TargetName),
                        response.ToJsonString(_options));
                    return null;
                case 1012002:
                    //没有按照规定的时间间隔发送心跳包
                    _logger.LogWithResponse(
                        () => _logger.LogWarning(
                            "uid {Uid} 给 {TargetUid}({TargetName}) 发送X心跳包失败，因为没有按照规定的时间间隔发送心跳包",
                            target.Uid,
                            target.TargetUid,
                            target.TargetName),
                        response.ToJsonString(_options));
                    return null;
                case 1012003:
                    //心跳包时间戳错误
                    _logger.LogWithResponse(
                        () => _logger.LogWarning("uid {Uid} 给 {TargetUid}({TargetName}) 发送X心跳包失败，时间戳错误",
                            target.Uid,
                            target.TargetUid,
                            target.TargetName),
                        response.ToJsonString(_options));
                    return null;
                default:
                    _logger.LogWithResponse(
                        () => _logger.LogError("uid {Uid} 给 {TargetUid}({TargetName}) 发送X心跳包失败",
                            target.Uid,
                            target.TargetUid,
                            target.TargetName),
                        response.ToJsonString(_options));
                    throw new LittleHeartException(Reason.RiskControl);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "uid {Uid} 给 {TargetUid}({TargetName}) 发送X心跳包时出现 HttpRequestException 异常，且重试多次后仍然出现异常",
                target.Uid,
                target.TargetUid,
                target.TargetName);
            throw new LittleHeartException(ex.Message, ex, Reason.RiskControl);
        }
        catch (LittleHeartException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "uid {Uid} 给 {TargetUid}({TargetName}) 发送X心跳包时发生 Json 异常",
                target.Uid,
                target.TargetUid,
                target.TargetName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "uid {Uid} 给 {TargetUid}({TargetName}) 发送X心跳包时发生意料之外的异常",
                target.Uid,
                target.TargetUid,
                target.TargetName);
            return null;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="target"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="LittleHeartException">
    /// <br/>Reason.UserCookieExpired
    /// <br/>Reason.RiskControl
    /// </exception>
    private async Task<int?> GetExpAsync(TargetModel target, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiService.GetExpAsync(target, cancellationToken);

            int code = (int)response["code"]!;
            bool withoutMedal = !(bool)response["data"]!["has_fans_medal"]!;
            switch (code)
            {
                case 0 when withoutMedal:
                    _logger.LogWithResponse(
                        () => _logger.LogWarning("uid {Uid} 未持有 {TargetUid}({TargetName}) 的粉丝牌",
                            target.Uid,
                            target.TargetUid,
                            target.TargetName),
                        response.ToJsonString(_options));
                    throw new LittleHeartException(Reason.WithoutMedal);
                case 0:
                    _logger.LogDebug("uid {Uid} 获取 {TargetUid}({TargetName}) 的粉丝牌经验成功",
                        target.Uid,
                        target.TargetUid,
                        target.TargetName);
                    return (int?)response["data"]!["my_fans_medal"]!["today_feed"];
                case -101:
                    _logger.LogWithResponse(
                        () => _logger.LogInformation("uid {Uid} 的cookie已过期",
                            target.Uid),
                        response.ToJsonString(_options));
                    throw new LittleHeartException(Reason.UserCookieExpired);
                case 19002009:
                    _logger.LogWithResponse(
                        () => _logger.LogWarning("uid {Uid} 查询 {TargetUid}({TargetName}) 的粉丝牌信息失败，需要重试",
                            target.Uid,
                            target.TargetUid,
                            target.TargetName),
                        response.ToJsonString(_options));
                    //即使出现19002009错误，response中仍然可能包含粉丝牌信息
                    return (int?)response["data"]?["my_fans_medal"]?["today_feed"];
                default:
                    _logger.LogWithResponse(
                        () => _logger.LogError("uid {Uid} 查询 {TargetUid}({TargetName}) 的粉丝牌信息失败",
                            target.Uid,
                            target.TargetUid,
                            target.TargetName),
                        response.ToJsonString(_options));
                    throw new LittleHeartException(Reason.RiskControl);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "uid {Uid} 查询 {TargetUid}({TargetName}) 的粉丝牌信息时出现 HttpRequestException 异常，且重试多次后仍然出现异常",
                target.Uid,
                target.TargetUid,
                target.TargetName);
            throw new LittleHeartException(ex.Message, ex, Reason.RiskControl);
        }
        catch (LittleHeartException ex) when (ex.Reason == Reason.WithoutMedal)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            db.Targets.Remove(target);
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (LittleHeartException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "uid {Uid} 获取 {TargetUid}({TargetName}) 粉丝牌经验时发生意料之外的异常",
                target.Uid,
                target.TargetUid,
                target.TargetName);
            return null;
        }
    }

    private bool IsCompleted(TargetModel target)
    {
        //只有经验>=1500或观看时长>=75分钟才判定为完成
        return target is not { Exp: < 1500, WatchedSeconds: < 75 * 60 };
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="target"></param>
    /// <param name="ePayload"></param>
    /// <param name="heartbeatData"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="LittleHeartException">
    /// <br/>Reason.UserCookieExpired
    /// <br/>Reason.RiskControl
    /// </exception>
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
                _logger.LogWarning(
                    "uid {Uid} 给 {TargetUid}({TargetName}) 发送X心跳包失败，停止继续发包，观看时长 {WatchedSeconds} 秒，获得经验 {Exp}",
                    target.Uid,
                    target.TargetUid,
                    target.TargetName,
                    target.WatchedSeconds,
                    target.Exp);
                return;
            }

            heartbeatData = data;
            interval = (int)heartbeatData["heartbeat_interval"]!;
            target.WatchedSeconds += interval;

            _logger.LogDebug("uid {Uid} 给 {TargetUid}({TargetName}) 发送X心跳包成功，当前观看时长 {WatchedSeconds} 秒",
                target.Uid,
                target.TargetUid,
                target.TargetName,
                target.WatchedSeconds);

            //每隔5分钟检查一次是否完成
            if (target.WatchedSeconds % 300 != 0)
            {
                continue;
            }

            //先获取经验
            int? exp = await GetExpAsync(target, cancellationToken);
            if (exp is null)
            {
                return;
            }

            target.Exp = exp.Value;
            //再根据经验和观看时长判断是否完成
            if (!IsCompleted(target))
            {
                continue;
            }

            _logger.LogInformation("uid {Uid} 在 {TargetUid}({TargetName}) 的任务完成，观看时长 {WatchedSeconds} 秒，获得经验 {Exp}",
                target.Uid,
                target.TargetUid,
                target.TargetName,
                target.WatchedSeconds,
                target.Exp);

            return;
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