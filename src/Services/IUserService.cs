using little_heart_bot_3.Data.Models;

namespace little_heart_bot_3.Services;

public interface IUserService
{
    public Task SendMessageAsync(UserModel user, CancellationToken cancellationToken = default);

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