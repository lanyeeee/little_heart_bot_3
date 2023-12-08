using System.Text.Json;
using little_heart_bot_3.Data;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;

namespace little_heart_bot_3.Services.Implements.App;

public class TargetService : Implements.TargetService
{
    public TargetService([FromKeyedServices("app:Logger")] ILogger logger,
        [FromKeyedServices("app:LittleHeartDbContext")]  LittleHeartDbContext db,
        JsonSerializerOptions options,
        HttpClient httpClient)
        : base(logger, db, options, httpClient)
    {
    }
}