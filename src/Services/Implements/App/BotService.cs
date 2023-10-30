using little_heart_bot_3.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;

namespace little_heart_bot_3.Services.Implements.App;

public class BotService : Implements.BotService
{
    public BotService([FromKeyedServices("app:Logger")] Logger logger, IBotRepository botRepository) : base(logger,
        botRepository)
    {
    }
}