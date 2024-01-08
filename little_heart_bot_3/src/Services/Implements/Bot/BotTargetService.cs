using System.Text.Json;

namespace little_heart_bot_3.Services.Implements.Bot;

public class BotTargetService : Implements.TargetService
{
    public BotTargetService(
        [FromKeyedServices("bot:Logger")] ILogger logger,
        JsonSerializerOptions options,
        HttpClient httpClient,
        IServiceProvider provider)
        : base(logger, options, httpClient, provider)
    {
    }
}