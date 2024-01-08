using System.Text.Json;

namespace little_heart_bot_3.Services.Implements.App;

public class AppMessageService : Implements.MessageService
{
    public AppMessageService(
        [FromKeyedServices("app:Logger")] ILogger logger,
        JsonSerializerOptions options,
        HttpClient httpClient,
        IServiceProvider provider)
        : base(logger, options, httpClient, provider)
    {
    }
}