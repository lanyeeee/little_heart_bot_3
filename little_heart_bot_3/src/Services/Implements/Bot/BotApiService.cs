using System.Text.Json;

namespace little_heart_bot_3.Services.Implements.Bot;

public sealed class BotApiService : ApiService
{
    public BotApiService(
        JsonSerializerOptions options,
        ILogger<BotHostedService> logger,
        IHttpClientFactory httpclientFactory) :
        base(logger, options, httpclientFactory)
    {
    }
}