using little_heart_bot_3.Data;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;

namespace little_heart_bot_3.Services.Implements.Bot;

public class UserService : Implements.UserService
{
    public UserService([FromKeyedServices("bot:Logger")] Logger logger,
        LittleHeartDbContext db,
        [FromKeyedServices("bot:MessageService")]
        IMessageService messageService,
        [FromKeyedServices("bot:TargetService")]
        ITargetService targetService)
        : base(logger, db, messageService, targetService)
    {
    }
}