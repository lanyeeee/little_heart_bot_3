using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace little_heart_bot_3.Services.Implements.Bot;

public class TargetService : Implements.TargetService
{
    public TargetService(
        [FromKeyedServices("bot:Logger")] ILogger logger,
        JsonSerializerOptions options,
        HttpClient httpClient)
        : base(logger, options, httpClient)
    {
    }
}