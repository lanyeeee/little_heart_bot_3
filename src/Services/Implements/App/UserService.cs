using little_heart_bot_3.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;

namespace little_heart_bot_3.Services.Implements.App;

public class UserService : Implements.UserService
{
    public UserService([FromKeyedServices("app:Logger")] Logger logger, IUserRepository userRepository,
        [FromKeyedServices("app:MessageService")]
        IMessageService messageService,
        [FromKeyedServices("app:TargetService")]
        ITargetService targetService) : base(logger, userRepository, messageService,
        targetService)
    {
    }
}