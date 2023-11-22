using little_heart_bot_3.Models;

namespace little_heart_bot_3.Repositories;

public interface IBotRepository
{
    public Task<BotModel?> GetBotAsync(CancellationToken cancellationToken = default);
}