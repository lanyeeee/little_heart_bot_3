using System.Text.Json.Nodes;
using little_heart_bot_3.Models;

namespace little_heart_bot_3.Services;

public interface IBotService
{
    public Task<IEnumerable<JsonNode?>?> GetSessionListAsync(BotModel botModel,
        CancellationToken cancellationToken = default);

    public Task<IEnumerable<JsonNode?>?> GetMessagesAsync(BotModel bot, UserModel user,
        CancellationToken cancellationToken = default);

    public Task UpdateSignAsync(BotModel botModel, CancellationToken cancellationToken = default);

    public Task<bool> SendMessageAsync(BotModel botModel, string content, string targetUid,
        CancellationToken cancellationToken = default);

    public Task<BotModel?> GetBotAsync(CancellationToken cancellationToken = default);
}