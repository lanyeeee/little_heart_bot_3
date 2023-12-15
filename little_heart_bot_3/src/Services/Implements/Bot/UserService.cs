using little_heart_bot_3.Data;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace little_heart_bot_3.Services.Implements.Bot;

public class UserService : Implements.UserService
{
    public UserService([FromKeyedServices("bot:Logger")] ILogger logger,
        [FromKeyedServices("bot:LittleHeartDbContext")]
        LittleHeartDbContext db,
        [FromKeyedServices("bot:MessageService")]
        IMessageService messageService,
        [FromKeyedServices("bot:TargetService")]
        ITargetService targetService,
        IServiceProvider provider)
        : base(logger, db, messageService, targetService, provider)
    {
    }
}