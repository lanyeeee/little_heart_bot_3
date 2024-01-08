using System.Text.Json;

namespace little_heart_bot_3.Services.Implements.App;

public class AppTargetService : Implements.TargetService
{
    public AppTargetService(
        [FromKeyedServices("app:Logger")] ILogger logger,
        JsonSerializerOptions options,
        HttpClient httpClient,
        IServiceProvider provider)
        : base(logger, options, httpClient, provider)
    {
    }
}