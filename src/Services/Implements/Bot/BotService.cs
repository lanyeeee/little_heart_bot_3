using System.Text.Json;
using little_heart_bot_3.Data;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;

namespace little_heart_bot_3.Services.Implements.Bot;

public class BotService : Implements.BotService
{
    public BotService([FromKeyedServices("bot:Logger")] Logger logger,
        LittleHeartDbContext db,
        JsonSerializerOptions options,
        HttpClient httpClient)
        : base(logger, db, options, httpClient)
    {
    }
}