using System.Text.Json.Nodes;
using little_heart_bot_3.Data.Models;
using little_heart_bot_3.Others;

namespace little_heart_bot_3.Services;

public interface IUserService
{
    /// <summary>
    /// 发送user的所有弹幕
    /// </summary>
    /// <param name="user"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="LittleHeartException">
    /// <br/>Reason.RiskControl
    /// </exception>
    public Task SendMessageAsync(UserModel user, CancellationToken cancellationToken = default);

    /// <summary>
    /// 观看user对应的直播间
    /// </summary>
    /// <param name="user"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="LittleHeartException">
    /// <br/>Reason.RiskControl
    /// </exception>
    public Task WatchLiveAsync(UserModel user, CancellationToken cancellationToken = default);

    /// <summary>
    /// 对应/config_all命令
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public string? GetConfigAllString(UserModel user);

    /// <summary>
    /// 对应/message_config 不带参数 的命令
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public string? GetAllMessageConfigString(UserModel user);

    /// <summary>
    /// 对应/message_config 参数为targetUid 的命令
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public string? GetSpecifyMessageConfigString(MessageModel message);

    /// <summary>
    /// 对应 /message_config 参数为all 的命令
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public List<string>? GetAllMessageConfigStringSplit(UserModel user);

    /// <summary>
    /// 对应/target_config 不带参数 的命令
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public string? GetAllTargetConfigString(UserModel user);

    /// <summary>
    /// 对应/target_config 参数为targetUid 的命令
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    public string? GetSpecifyTargetConfigString(TargetModel target);

    /// <summary>
    /// 对应/target_config 参数为all 的命令
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public List<string>? GetAllTargetConfigStringSplit(UserModel user);
}