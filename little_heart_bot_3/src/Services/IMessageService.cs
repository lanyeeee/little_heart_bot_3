using little_heart_bot_3.Data.Models;
using little_heart_bot_3.Others;

namespace little_heart_bot_3.Services;

public interface IMessageService
{
    /// <summary>
    /// 发送message到对应的直播间
    /// </summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="LittleHeartException">
    /// <br/>Reason.UserCookieExpired
    /// <br/>Reason.RiskControl
    /// </exception>
    public Task SendAsync(MessageModel message, CancellationToken cancellationToken = default);


    /// <summary>
    /// 给message对应的直播间点赞
    /// </summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="LittleHeartException">
    /// <br/>Reason.UserCookieExpired, 
    /// <br/>Reason.RiskControl
    /// </exception>
    public Task ThumbsUpAsync(MessageModel message, CancellationToken cancellationToken = default);
}