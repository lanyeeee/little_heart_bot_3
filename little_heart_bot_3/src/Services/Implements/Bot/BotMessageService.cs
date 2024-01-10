using System.Text.Json;
using little_heart_bot_3.Data;
using Microsoft.EntityFrameworkCore;

namespace little_heart_bot_3.Services.Implements.Bot;

public class BotMessageService : Implements.MessageService
{
    public BotMessageService(
        [FromKeyedServices("bot:Logger")] ILogger logger,
        JsonSerializerOptions options,
        HttpClient httpClient,
        IDbContextFactory<LittleHeartDbContext> factory)
        : base(logger, options, httpClient, factory)
    {
    }
}