using System.Text.Json;

namespace little_heart_bot_3.Services.Implements.Bot;

public class BotMessageService : Implements.MessageService
{
    public BotMessageService(
        [FromKeyedServices("bot:Logger")] ILogger logger,
        JsonSerializerOptions options,
        HttpClient httpClient,
        IServiceProvider provider)
        : base(logger, options, httpClient, provider)
    {
    }
}