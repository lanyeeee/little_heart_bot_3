using little_heart_bot_3.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;

namespace little_heart_bot_3.Services.Implements.Bot;

public class TargetService : Implements.TargetService
{
    public TargetService([FromKeyedServices("bot:Logger")] Logger logger, ITargetRepository targetRepository)
        : base(logger, targetRepository)
    {
    }
}