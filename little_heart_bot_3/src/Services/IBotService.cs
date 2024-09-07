using System.Text.Json.Nodes;
using little_heart_bot_3.Data.Models;
using little_heart_bot_3.Others;

namespace little_heart_bot_3.Services;

public interface IBotService
{
    /// <summary>
    /// 获取bot的私信列表
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="LittleHeartException">
    /// <br/>Reason.RiskControl
    /// <br/>Reason.BotCookieExpired
    /// </exception>
    public Task<IEnumerable<JsonNode?>?> GetSessionListAsync(
        BotModel bot,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取bot与user之间的私信记录
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="user"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="LittleHeartException">
    /// <br/>Reason.RiskControl
    /// </exception>
    public Task<IEnumerable<JsonNode?>?> GetPrivateMessagesAsync(
        BotModel bot,
        UserModel user,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新签名
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="sign"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="LittleHeartException">
    /// <br/>Reason.BotCookieExpired
    /// <br/>Reason.RiskControl
    /// </exception>
    public Task UpdateSignAsync(BotModel bot, string sign, CancellationToken cancellationToken = default);

    /// <summary>
    /// bot向user发送私信，内容为content
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="user"></param>
    /// <param name="content"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="LittleHeartException">
    /// <br/>Reason.RiskControl
    /// </exception>
    public Task SendPrivateMessageAsync(
        BotModel bot,
        UserModel user,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 处理收到的命令
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="user"></param>
    /// <param name="command"></param>
    /// <param name="parameter"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="LittleHeartException">
    /// <br/>Reason.RiskControl
    /// </exception>
    public Task HandleCommandAsync(
        BotModel bot,
        UserModel user,
        string command,
        string? parameter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取uid对应用户的详细信息
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="uid"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="LittleHeartException">
    /// <br/>Reason.RiskControl
    /// <br/>Reason.UserCookieExpired
    /// </exception>
    public Task<JsonNode?> GetOtherUserInfoAsync(BotModel bot, long uid,
        CancellationToken cancellationToken = default);
}