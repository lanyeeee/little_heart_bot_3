using System.Text.Json;

namespace little_heart_bot_3.Services.Implements.App;

public class AppUserService : Implements.UserService
{
    public AppUserService(
        [FromKeyedServices("app:Logger")] ILogger logger,
        [FromKeyedServices("app:MessageService")]
        IMessageService messageService,
        [FromKeyedServices("app:TargetService")]
        ITargetService targetService,
        JsonSerializerOptions options,
        HttpClient httpClient,
        IServiceProvider provider)
        : base(logger, messageService, targetService, options, httpClient, provider)
    {
    }
}