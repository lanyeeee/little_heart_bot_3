using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace little_heart_bot_3.Services.Implements.App;

public class UserService : Implements.UserService
{
    public UserService(
        IServiceProvider provider,
        [FromKeyedServices("app:Logger")] ILogger logger,
        [FromKeyedServices("app:MessageService")]
        IMessageService messageService,
        [FromKeyedServices("app:TargetService")]
        ITargetService targetService,
        JsonSerializerOptions options,
        HttpClient httpClient)
        : base(provider, logger, messageService, targetService, options, httpClient)
    {
    }
}