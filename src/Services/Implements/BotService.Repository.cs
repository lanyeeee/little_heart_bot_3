using little_heart_bot_3.Models;

namespace little_heart_bot_3.Services.Implements;

public partial class BotService
{
    public Task<BotModel?> GetBotAsync(CancellationToken cancellationToken = default)
    {
        return _botRepository.GetBotAsync(cancellationToken);
    }
}