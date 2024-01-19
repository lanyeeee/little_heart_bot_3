using System.Text.Json;
using little_heart_bot_3.Data;
using Microsoft.EntityFrameworkCore;

namespace little_heart_bot_3.Services.Implements.App;

public class AppTargetService : TargetService
{
    public AppTargetService(
        ILogger<AppHostedService> logger,
        JsonSerializerOptions options,
        [FromKeyedServices("app:ApiService")] IApiService apiService,
        IDbContextFactory<LittleHeartDbContext> dbContextFactory)
        : base(logger, options, apiService, dbContextFactory)
    {
    }
}