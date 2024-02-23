using little_heart_bot_3.Data.Models;
using little_heart_bot_3.Others;


namespace little_heart_bot_3.Services;

public interface ITargetService
{
    /// <summary>
    /// 开始观看target对应的直播间
    /// </summary>
    /// <param name="target"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="LittleHeartException">
    /// <br/>Reason.UserCookieExpired
    /// <br/>Reason.RiskControl
    /// </exception>
    public Task StartAsync(TargetModel target, CancellationToken cancellationToken = default);
}