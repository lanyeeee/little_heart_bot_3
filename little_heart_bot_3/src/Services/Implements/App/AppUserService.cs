using System.Text.Json;
using little_heart_bot_3.Data;
using Microsoft.EntityFrameworkCore;

namespace little_heart_bot_3.Services.Implements.App;

public class AppUserService : Implements.UserService
{
    public AppUserService(
        [FromKeyedServices("app:Logger")] ILogger logger,
        [FromKeyedServices("app:MessageService")]
        IMessageService messageService,
        [FromKeyedServices("app:TargetService")]
        ITargetService targetService,
        JsonSerializerOptions options,
        IHttpClientFactory httpClientFactory,
        IDbContextFactory<LittleHeartDbContext> dbContextFactory)
        : base(logger, messageService, targetService, options, httpClientFactory, dbContextFactory)
    {
    }
}