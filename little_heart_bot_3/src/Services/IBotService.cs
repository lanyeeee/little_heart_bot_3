using System.Text.Json.Nodes;
using little_heart_bot_3.Data.Models;

namespace little_heart_bot_3.Services;

public interface IBotService
{
    /// <summary>
    /// 获取bot的私信列表
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<IEnumerable<JsonNode?>?> GetSessionListAsync(BotModel bot,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取bot与user之间的私信记录
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="user"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<IEnumerable<JsonNode?>?> GetPrivateMessagesAsync(BotModel bot, UserModel user,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新签名
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task UpdateSignAsync(BotModel bot, CancellationToken cancellationToken = default);

    /// <summary>
    /// bot向user发送私信，内容为content
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="content"></param>
    /// <param name="user"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<bool> SendPrivateMessageAsync(BotModel bot, string content, UserModel user,
        CancellationToken cancellationToken = default);
}