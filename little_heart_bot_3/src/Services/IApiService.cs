using System.Text.Json.Nodes;
using little_heart_bot_3.Data.Models;

namespace little_heart_bot_3.Services;

public interface IApiService
{
    /// <summary>
    /// 检验用户的cookie是否有效
    /// </summary>
    /// <param name="user"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>由response body反序列化得来</returns>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="HttpRequestException"></exception>
    public Task<JsonNode> VerifyCookiesAsync(UserModel user, CancellationToken cancellationToken = default);


    /// <summary>
    /// 
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="user"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="HttpRequestException"></exception>
    public Task<JsonNode> GetPrivateMessagesAsync(BotModel bot,
        UserModel user,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="sign"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="HttpRequestException"></exception>
    public Task<JsonNode> UpdateSignAsync(BotModel bot, string sign, CancellationToken cancellationToken = default);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="user"></param>
    /// <param name="content"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="HttpRequestException"></exception>
    public Task<JsonNode> SendPrivateMessageAsync(BotModel bot,
        UserModel user,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="HttpRequestException"></exception>
    public Task<JsonNode> GetNormalSessionListAsync(BotModel bot,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="HttpRequestException"></exception>
    public Task<JsonNode> GetBlockedSessionListAsync(BotModel bot,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="HttpRequestException"></exception>
    public Task<JsonNode> ThumbsUpAsync(MessageModel message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="HttpRequestException"></exception>
    public Task<JsonNode> PostMessageAsync(MessageModel message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="target"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="HttpRequestException"></exception>
    public Task<JsonNode> GetExpAsync(TargetModel target, CancellationToken cancellationToken = default);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="target"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="HttpRequestException"></exception>
    public Task<JsonNode> GetEPayloadAsync(TargetModel target,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="target"></param>
    /// <param name="ePayload"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>xPayload</returns>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="HttpRequestException"></exception>
    public Task<JsonNode> PostEAsync(TargetModel target,
        Dictionary<string, string> ePayload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="target"></param>
    /// <param name="payload"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="HttpRequestException"></exception>
    public Task<JsonNode> PostXAsync(TargetModel target,
        Dictionary<string, string> payload,
        CancellationToken cancellationToken = default);


    /// <summary>
    /// 
    /// </summary>
    /// <param name="user"></param>
    /// <param name="uid"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="OperationCanceledException"></exception>
    /// <exception cref="HttpRequestException"></exception>
    public Task<JsonNode> GetOtherUserInfoAsync(UserModel user,
        long uid,
        CancellationToken cancellationToken = default);
}