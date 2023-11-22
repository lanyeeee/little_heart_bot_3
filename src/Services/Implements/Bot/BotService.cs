using little_heart_bot_3.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;

namespace little_heart_bot_3.Services.Implements.Bot;

public class BotService : Implements.BotService
{
    public BotService([FromKeyedServices("bot:Logger")] Logger logger, IBotRepository botRepository) : base(logger,
        botRepository)
    {
    }
}