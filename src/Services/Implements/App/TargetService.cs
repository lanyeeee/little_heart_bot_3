using little_heart_bot_3.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;

namespace little_heart_bot_3.Services.Implements.App;

public class TargetService : Implements.TargetService
{
    public TargetService([FromKeyedServices("app:Logger")] Logger logger, ITargetRepository targetRepository)
        : base(logger, targetRepository)
    {
    }
}