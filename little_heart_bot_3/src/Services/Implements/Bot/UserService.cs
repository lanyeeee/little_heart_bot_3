using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace little_heart_bot_3.Services.Implements.Bot;

public class UserService : Implements.UserService
{
    public UserService([FromKeyedServices("bot:Logger")] ILogger logger,
        [FromKeyedServices("bot:MessageService")]
        IMessageService messageService,
        [FromKeyedServices("bot:TargetService")]
        JsonSerializerOptions options,
        HttpClient httpClient,
        IServiceProvider provider)
        : base(logger, messageService, options, httpClient, provider)
    {
    }
}