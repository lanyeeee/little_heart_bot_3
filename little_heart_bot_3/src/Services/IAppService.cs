using little_heart_bot_3.Others;

namespace little_heart_bot_3.Services;

public interface IAppService
{
    /// <summary>
    /// 验证用户的cookie
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="LittleHeartException">
    /// <br/>Reason.RiskControl
    /// </exception>
    Task VerifyCookiesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送直播间弹幕
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="LittleHeartException">
    /// <br/>Reason.RiskControl
    /// </exception>
    Task SendMessageAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 观看直播
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="LittleHeartException">
    /// <br/>Reason.RiskControl
    /// </exception>
    Task WatchLiveAsync(CancellationToken cancellationToken = default);
}