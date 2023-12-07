using System.Text.Json;
using little_heart_bot_3.Data;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;

namespace little_heart_bot_3.Services.Implements.App;

public class MessageService : Implements.MessageService
{
    public MessageService([FromKeyedServices("app:Logger")] Logger logger,
        LittleHeartDbContext db,
        JsonSerializerOptions options,
        HttpClient httpClient)
        : base(logger, db, options, httpClient)
    {
    }
}