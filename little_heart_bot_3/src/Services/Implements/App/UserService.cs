using little_heart_bot_3.Data;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;

namespace little_heart_bot_3.Services.Implements.App;

public class UserService : Implements.UserService
{
    public UserService([FromKeyedServices("app:Logger")] ILogger logger,
        [FromKeyedServices("app:LittleHeartDbContext")]  LittleHeartDbContext db,
        [FromKeyedServices("app:MessageService")]
        IMessageService messageService,
        [FromKeyedServices("app:TargetService")]
        ITargetService targetService)
        : base(logger, db, messageService, targetService)
    {
    }
}