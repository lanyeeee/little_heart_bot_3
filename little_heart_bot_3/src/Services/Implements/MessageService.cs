﻿using System.Text.Json;
using little_heart_bot_3.Data;
using little_heart_bot_3.Data.Models;
using little_heart_bot_3.Others;
using Microsoft.EntityFrameworkCore;

namespace little_heart_bot_3.Services.Implements;

public abstract class MessageService : IMessageService
{
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _options;
    private readonly IApiService _apiService;
    private readonly IDbContextFactory<LittleHeartDbContext> _dbContextFactory;


    protected MessageService(
        ILogger logger,
        JsonSerializerOptions options,
        IApiService apiService,
        IDbContextFactory<LittleHeartDbContext> dbContextFactory)
    {
        _logger = logger;
        _options = options;
        _apiService = apiService;
        _dbContextFactory = dbContextFactory;
    }

    public async Task SendAsync(MessageModel message, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.Messages.Attach(message);
        try
        {
            var response = await _apiService.PostMessageAsync(message, cancellationToken);

            message.Completed = true;
            message.Code = (int)response["code"]!;
            message.Response = response.ToJsonString(_options);

            switch (message.Code)
            {
                case 0:
                    _logger.LogInformation("uid {Uid} 给 {TargetUid}({TargetName}) 发送弹幕成功",
                        message.Uid,
                        message.TargetUid,
                        message.TargetName);
                    break;
                //风控
                case -412:
                    _logger.LogWithResponse(
                        () => _logger.LogWarning("uid {Uid} 给 {TargetUid}({TargetName}) 发送弹幕失败，因为风控",
                            message.Uid,
                            message.TargetUid,
                            message.TargetName),
                        response.ToJsonString(_options));
                    throw new LittleHeartException(response.ToJsonString(_options), Reason.RiskControl);
                //Cookie过期
                case -111 or -101:
                    _logger.LogWithResponse(
                        () => _logger.LogWarning("uid {Uid} 给 {TargetUid}({TargetName}) 发送弹幕失败，因为Cookie过期",
                            message.Uid,
                            message.TargetUid,
                            message.TargetName),
                        response.ToJsonString(_options));
                    throw new LittleHeartException(response.ToJsonString(_options), Reason.UserCookieExpired);
                //可能是等级墙，也可能是全体禁言
                case -403:
                    _logger.LogWithResponse(
                        () => _logger.LogInformation("uid {Uid} 给 {TargetUid}({TargetName}) 发送弹幕失败，因为主播开启了禁言",
                            message.Uid,
                            message.TargetUid,
                            message.TargetName),
                        response.ToJsonString(_options));
                    break;
                //似乎跟Up主的身份有关系
                case 11000:
                    _logger.LogWithResponse(
                        () => _logger.LogInformation("uid {Uid} 给 {TargetUid}({TargetName}) 发送弹幕失败，原因未知",
                            message.Uid,
                            message.TargetUid,
                            message.TargetName),
                        response.ToJsonString(_options));
                    break;
                //发弹幕的频率过高
                case 10030 or 10031:
                    _logger.LogWithResponse(
                        () => _logger.LogInformation("uid {Uid} 给 {TargetUid}({TargetName}) 发送弹幕失败，因为发送弹幕的频率过高",
                            message.Uid,
                            message.TargetUid,
                            message.TargetName),
                        response.ToJsonString(_options));
                    break;
                //用户已将主播拉黑
                case 10023:
                    _logger.LogWithResponse(
                        () => _logger.LogInformation("uid {Uid} 给 {TargetUid}({TargetName}) 发送弹幕失败，因为用户已将主播拉黑",
                            message.Uid,
                            message.TargetUid,
                            message.TargetName),
                        response.ToJsonString(_options));
                    break;
                //用户已在本房间被禁言
                case 1003:
                    _logger.LogWithResponse(
                        () => _logger.LogInformation("uid {Uid} 给 {TargetUid}({TargetName}) 发送弹幕失败，因为用户已在本房间被禁言",
                            message.Uid,
                            message.TargetUid,
                            message.TargetName),
                        response.ToJsonString(_options));
                    break;
                //因主播隐私设置，暂无法发送弹幕
                case 10024:
                    _logger.LogWithResponse(
                        () => _logger.LogInformation("uid {Uid} 给 {TargetUid}({TargetName}) 发送弹幕失败，因为主播隐私设置，暂无法发送弹幕",
                            message.Uid,
                            message.TargetUid,
                            message.TargetName),
                        response.ToJsonString(_options));
                    break;
                default:
                    _logger.LogWithResponse(
                        () => _logger.LogError("uid {Uid} 给 {TargetUid}({TargetName}) 发送弹幕失败，预料之外的错误",
                            message.Uid,
                            message.TargetUid,
                            message.TargetName),
                        response.ToJsonString(_options));
                    throw new LittleHeartException(response.ToJsonString(_options), Reason.RiskControl);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "uid {Uid} 给 {TargetUid}({TargetName}) 发送弹幕时出现 HttpRequestException 异常，重试多次后依然失败",
                message.Uid,
                message.TargetUid,
                message.TargetName);
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
            _logger.LogCritical(ex, "uid {Uid} 给 {TargetUid}({TargetName}) 发送消息时出现预料之外的错误",
                message.Uid,
                message.TargetUid,
                message.TargetName);
        }
        finally
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ThumbsUpAsync(MessageModel message, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiService.ThumbsUpAsync(message, cancellationToken);

            int code = (int)response["code"]!;
            switch (code)
            {
                case 0:
                    _logger.LogInformation("uid {Uid} 给 {TargetUid}({TargetName}) 点赞成功",
                        message.Uid,
                        message.TargetUid,
                        message.TargetName);
                    break;
                case -111 or -101:
                    _logger.LogWithResponse(
                        () => _logger.LogWarning("uid {Uid} 给 {TargetUid}({TargetName}) 点赞失败，因为Cookie错误或已过期",
                            message.Uid,
                            message.TargetUid,
                            message.TargetName),
                        response.ToJsonString(_options));
                    throw new LittleHeartException(Reason.UserCookieExpired);
                default:
                    _logger.LogWithResponse(
                        () => _logger.LogError("uid {Uid} 给 {TargetUid}({TargetName}) 点赞失败，预料之外的错误",
                            message.Uid,
                            message.TargetUid,
                            message.TargetName),
                        response.ToJsonString(_options));
                    throw new LittleHeartException(Reason.RiskControl);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "uid {Uid} 给 {TargetUid}({TargetName}) 点赞时出现 HttpRequestException 异常，重试多次后依然失败",
                message.Uid,
                message.TargetUid,
                message.TargetName);
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
            _logger.LogCritical(ex, "uid {Uid} 给 {TargetUid}({TargetName}) 点赞时出现预料之外的错误",
                message.Uid,
                message.TargetUid,
                message.TargetName);
        }
    }
}