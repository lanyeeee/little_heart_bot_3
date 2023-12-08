using System.Text.Json;
using little_heart_bot_3.Data;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;

namespace little_heart_bot_3.Services.Implements.Bot;

public class TargetService : Implements.TargetService
{
    public TargetService([FromKeyedServices("bot:Logger")] ILogger logger,
        [FromKeyedServices("bot:LittleHeartDbContext")]
        LittleHeartDbContext db,
        JsonSerializerOptions options,
        HttpClient httpClient)
        : base(logger, db, options, httpClient)
    {
    }
}