using System.Text.Json;
using little_heart_bot_3.Data;
using Microsoft.EntityFrameworkCore;

namespace little_heart_bot_3.Services.Implements.App;

public class AppUserService : UserService
{
    public AppUserService(
        ILogger<AppHostedService> logger,
        JsonSerializerOptions options,
        [FromKeyedServices("app:MessageService")]
        IMessageService messageService,
        [FromKeyedServices("app:TargetService")]
        ITargetService targetService,
        [FromKeyedServices("app:ApiService")] IApiService apiService,
        IDbContextFactory<LittleHeartDbContext> dbContextFactory)
        : base(logger, options, messageService, targetService, apiService, dbContextFactory)
    {
    }
}