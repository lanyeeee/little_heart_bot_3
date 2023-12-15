using System.Text.Json;
using little_heart_bot_3.Data;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace little_heart_bot_3.Services.Implements.App;

public class UserService : Implements.UserService
{
    public UserService([FromKeyedServices("app:Logger")] ILogger logger,
        [FromKeyedServices("app:LittleHeartDbContext")]
        LittleHeartDbContext db,
        [FromKeyedServices("app:MessageService")]
        IMessageService messageService,
        [FromKeyedServices("app:TargetService")]
        ITargetService targetService,
        JsonSerializerOptions options,
        HttpClient httpClient,
        IServiceProvider provider)
        : base(logger, db, messageService, targetService, options, httpClient, provider)
    {
    }
}