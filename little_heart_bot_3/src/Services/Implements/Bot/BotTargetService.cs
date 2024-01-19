using System.Text.Json;
using little_heart_bot_3.Data;
using Microsoft.EntityFrameworkCore;

namespace little_heart_bot_3.Services.Implements.Bot;

public class BotTargetService : TargetService
{
    public BotTargetService(
        ILogger<BotHostedService> logger,
        JsonSerializerOptions options,
        [FromKeyedServices("bot:ApiService")] IApiService apiService,
        IDbContextFactory<LittleHeartDbContext> dbContextFactory)
        : base(logger, options, apiService, dbContextFactory)
    {
    }
}