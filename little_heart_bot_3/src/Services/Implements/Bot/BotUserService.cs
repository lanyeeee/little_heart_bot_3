using System.Text.Json;
using little_heart_bot_3.Data;
using Microsoft.EntityFrameworkCore;

namespace little_heart_bot_3.Services.Implements.Bot;

public class BotUserService : Implements.UserService
{
    public BotUserService(
        [FromKeyedServices("bot:Logger")] ILogger logger,
        [FromKeyedServices("bot:MessageService")]
        IMessageService messageService,
        [FromKeyedServices("bot:TargetService")]
        ITargetService targetService,
        JsonSerializerOptions options,
        HttpClient httpClient,
        IDbContextFactory<LittleHeartDbContext> factory)
        : base(logger, messageService, targetService, options, httpClient, factory)
    {
    }
}