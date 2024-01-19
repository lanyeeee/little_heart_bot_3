using System.Text.Json;

namespace little_heart_bot_3.Services.Implements.App;

public class AppApiService : ApiService
{
    public AppApiService(
        ILogger<AppHostedService> logger,
        JsonSerializerOptions options,
        IHttpClientFactory httpclientFactory)
        : base(logger, options, httpclientFactory)
    {
    }
}