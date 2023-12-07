using System.Text.Json.Nodes;
using little_heart_bot_3.Data.Models;

namespace little_heart_bot_3.Services;

public interface IBotService
{
    public Task<IEnumerable<JsonNode?>?> GetSessionListAsync(BotModel botModel,
        CancellationToken cancellationToken = default);

    public Task<IEnumerable<JsonNode?>?> GetMessagesAsync(BotModel bot, UserModel user,
        CancellationToken cancellationToken = default);

    public Task UpdateSignAsync(BotModel botModel, CancellationToken cancellationToken = default);

    public Task<bool> SendMessageAsync(BotModel botModel, string content, UserModel user,
        CancellationToken cancellationToken = default);
}