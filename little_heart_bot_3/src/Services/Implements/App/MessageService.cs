using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace little_heart_bot_3.Services.Implements.App;

public class MessageService : Implements.MessageService
{
    public MessageService(
        [FromKeyedServices("app:Logger")] ILogger logger,
        JsonSerializerOptions options,
        HttpClient httpClient)
        : base(logger, options, httpClient)
    {
    }
}