using System.Text.Json;
using little_heart_bot_3.Data;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;

namespace little_heart_bot_3.Services.Implements.Bot;

public class MessageService : Implements.MessageService
{
    public MessageService([FromKeyedServices("bot:Logger")] ILogger logger,
        LittleHeartDbContext db,
        JsonSerializerOptions options,
        HttpClient httpClient)
        : base(logger, db, options, httpClient)
    {
    }
}