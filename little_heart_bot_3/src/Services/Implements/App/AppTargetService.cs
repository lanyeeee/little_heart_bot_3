using System.Text.Json;
using little_heart_bot_3.Data;
using Microsoft.EntityFrameworkCore;

namespace little_heart_bot_3.Services.Implements.App;

public class AppTargetService : Implements.TargetService
{
    public AppTargetService(
        [FromKeyedServices("app:Logger")] ILogger logger,
        JsonSerializerOptions options,
        HttpClient httpClient,
        IDbContextFactory<LittleHeartDbContext> factory)
        : base(logger, options, httpClient, factory)
    {
    }
}