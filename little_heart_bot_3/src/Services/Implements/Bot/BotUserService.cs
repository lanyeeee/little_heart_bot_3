using System.Text.Json;
using little_heart_bot_3.Data;
using Microsoft.EntityFrameworkCore;

namespace little_heart_bot_3.Services.Implements.Bot;

public sealed class BotUserService : UserService
{
    public BotUserService(
        ILogger<BotHostedService> logger,
        JsonSerializerOptions options,
        [FromKeyedServices("bot:MessageService")]
        IMessageService messageService,
        [FromKeyedServices("bot:TargetService")]
        ITargetService targetService,
        [FromKeyedServices("bot:ApiService")] IApiService apiService,
        IDbContextFactory<LittleHeartDbContext> dbContextFactory)
        : base(logger, options, messageService, targetService, apiService, dbContextFactory)
    {
    }
}