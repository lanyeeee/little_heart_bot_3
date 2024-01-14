using System.Text.Json;
using little_heart_bot_3.Data;
using Microsoft.EntityFrameworkCore;

namespace little_heart_bot_3.Services.Implements.Bot;

public class BotMessageService : Implements.MessageService
{
    public BotMessageService(
        ILogger<BotHostedService> logger,
        JsonSerializerOptions options,
        IHttpClientFactory httpClientFactory,
        IDbContextFactory<LittleHeartDbContext> dbContextFactory)
        : base(logger, options, httpClientFactory, dbContextFactory)
    {
    }
}