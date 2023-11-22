using little_heart_bot_3.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;

namespace little_heart_bot_3.Services.Implements.App;

public class MessageService : Implements.MessageService
{
    public MessageService([FromKeyedServices("app:Logger")] Logger logger, IMessageRepository messageRepository) : base(
        logger, messageRepository)
    {
    }
}