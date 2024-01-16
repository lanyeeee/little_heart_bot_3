using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using little_heart_bot_3.Crypto;
using little_heart_bot_3.Data;
using little_heart_bot_3.Data.Models;
using little_heart_bot_3.Others;
using Microsoft.EntityFrameworkCore;

namespace little_heart_bot_3.Services.Implements;

public class TargetService : ITargetService
{
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _options;
    private readonly HttpClient _httpClient;
    private readonly IDbContextFactory<LittleHeartDbContext> _dbContextFactory;


    public TargetService(ILogger logger,
        JsonSerializerOptions options,
        IHttpClientFactory httpClientFactory,
        IDbContextFactory<LittleHeartDbContext> dbContextFactory)
    {
        _logger = logger;
        _options = options;
        _httpClient = httpClientFactory.CreateClient("global");
        _dbContextFactory = dbContextFactory;
    }

    public async Task StartAsync(TargetModel target, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
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

        _logger.LogDebug("uid {Uid} 在 {TargetName} 直播间的经验为 {Exp}",
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
        _logger.LogDebug("uid {Uid} 开始观看 {TargetName} 的直播", target.Uid, target.TargetName);
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
        await using var db = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
        db.Targets.Attach(target);

        var uri = new Uri(
            $"https://api.live.bilibili.com/xlive/web-room/v1/index/getInfoByRoom?&room_id={target.RoomId}");
        var requestMessage = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = uri
        }.SetRetryCallback((outcome, retryDelay, retryCount) =>
        {
            _logger.LogDebug(outcome.Exception,
                "获取E心跳包的payload时遇到异常，准备在 {RetryDelay} 秒后进行第 {RetryCount} 次重试",
                retryDelay.TotalSeconds,
                retryCount);
        });

        HttpResponseMessage responseMessage = await _httpClient.SendAsync(requestMessage, cancellationToken);
        JsonNode response = (await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, cancellationToken))!;

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
        try
        {
            HttpRequestMessage requestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://live-trace.bilibili.com/xlive/data-interface/v1/x25Kn/E"),
                Headers = { { "Cookie", target.UserModel.Cookie } },
                Content = new FormUrlEncodedContent(ePayload)
            }.SetRetryCallback((outcome, retryDelay, retryCount) =>
            {
                _logger.LogDebug(outcome.Exception,
                    "uid {Uid} 发送E心跳包时遇到异常，准备在 {RetryDelay} 秒后进行第 {RetryCount} 次重试",
                    target.Uid,
                    retryDelay.TotalSeconds,
                    retryCount);
            });

            HttpResponseMessage responseMessage = await _httpClient.SendAsync(requestMessage, cancellationToken);
            var response = (await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, cancellationToken))!;

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
    }

    private async Task<JsonNode?> PostXAsync(
        TargetModel target,
        Dictionary<string, string> payload,
        CancellationToken cancellationToken = default)
    {
        try
        {
            HttpRequestMessage requestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://live-trace.bilibili.com/xlive/data-interface/v1/x25Kn/X"),
                Headers = { { "Cookie", target.UserModel.Cookie } },
                Content = new FormUrlEncodedContent(payload)
            }.SetRetryCallback((outcome, retryDelay, retryCount) =>
            {
                _logger.LogDebug(outcome.Exception,
                    "uid {Uid} 发送X心跳包时遇到异常，准备在 {RetryDelay} 秒后进行第 {RetryCount} 次重试",
                    target.Uid,
                    retryDelay.TotalSeconds,
                    retryCount);
            });

            HttpResponseMessage responseMessage = await _httpClient.SendAsync(requestMessage, cancellationToken);
            var response = (await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, cancellationToken))!;

            int code = (int)response["code"]!;
            if (code == -504)
            {
                _logger.LogWithResponse(
                    () => _logger.LogWarning("uid {Uid} 给 {TargetName} 发送X心跳包失败，服务器调用超时",
                        target.Uid,
                        target.TargetName),
                    response.ToJsonString(_options));
                throw new HttpRequestException(response["message"]!.ToString());
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
    }

    private async Task<int?> GetExpAsync(TargetModel target, CancellationToken cancellationToken = default)
    {
        var uri = new Uri(
            $"https://api.live.bilibili.com/xlive/app-ucenter/v1/fansMedal/fans_medal_info?target_id={target.TargetUid}");

        try
        {
            var requestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = uri,
                Headers = { { "cookie", target.UserModel.Cookie } }
            }.SetRetryCallback((outcome, retryDelay, retryCount) =>
            {
                _logger.LogDebug(outcome.Exception,
                    "uid {Uid} 获取 {TargetName} 粉丝牌经验时遇到异常，准备在 {RetryDelay} 秒后进行第 {RetryCount} 次重试",
                    target.Uid,
                    target.TargetName,
                    retryDelay.TotalSeconds,
                    retryCount);
            });

            HttpResponseMessage responseMessage = await _httpClient.SendAsync(requestMessage, cancellationToken);
            var response = (await responseMessage.Content.ReadFromJsonAsync<JsonNode>(_options, cancellationToken))!;

            int code = (int)response["code"]!;
            if (code == -101)
            {
                _logger.LogWithResponse(
                    () => _logger.LogInformation("uid {Uid} 的cookie已过期",
                        target.Uid),
                    response.ToJsonString(_options));
                throw new LittleHeartException(Reason.CookieExpired);
            }
            else if (code == 19002009 && response["data"]?["my_fans_medal"]?["today_feed"] is null)
            {
                throw new HttpRequestException("获取粉丝勋章数据错误，请重试！");
            }
            else if (code != 0)
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
        }
        catch (LittleHeartException ex)
        {
            switch (ex.Reason)
            {
                case Reason.WithoutMedal:
                {
                    await using var db = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
                    db.Targets.Remove(target);
                    await db.SaveChangesAsync(CancellationToken.None);
                    return null;
                }
                default:
                    throw;
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
                _logger.LogError("因为 uid {Uid} 给 {TargetName} 发送X心跳包失败，停止继续发包，当前观看时长 {WatchedSeconds} 秒",
                    target.Uid,
                    target.TargetName,
                    target.WatchedSeconds);
                return;
            }

            heartbeatData = data;

            interval = (int)heartbeatData["heartbeat_interval"]!;
            await using var db = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
            db.Targets.Attach(target);
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