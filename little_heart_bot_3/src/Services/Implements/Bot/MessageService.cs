using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace little_heart_bot_3.Services.Implements.Bot;

public class MessageService : Implements.MessageService
{
    public MessageService(
        IServiceProvider provider,
        [FromKeyedServices("bot:Logger")] ILogger logger,
        JsonSerializerOptions options,
        HttpClient httpClient)
        : base(provider, logger, options, httpClient)
    {
    }
}