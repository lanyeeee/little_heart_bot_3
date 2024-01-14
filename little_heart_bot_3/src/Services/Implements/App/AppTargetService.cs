using System.Text.Json;
using little_heart_bot_3.Data;
using Microsoft.EntityFrameworkCore;

namespace little_heart_bot_3.Services.Implements.App;

public class AppTargetService : Implements.TargetService
{
    public AppTargetService(
        ILogger<AppHostedService> logger,
        JsonSerializerOptions options,
        IHttpClientFactory httpClientFactory,
        IDbContextFactory<LittleHeartDbContext> dbContextFactory)
        : base(logger, options, httpClientFactory, dbContextFactory)
    {
    }
}