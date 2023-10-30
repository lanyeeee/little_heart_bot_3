using little_heart_bot_3.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;

namespace little_heart_bot_3.Services.Implements.Bot;

public class MessageService : Implements.MessageService
{
    public MessageService([FromKeyedServices("bot:Logger")] Logger logger, IMessageRepository messageRepository) : base(
        logger, messageRepository)
    {
    }
}