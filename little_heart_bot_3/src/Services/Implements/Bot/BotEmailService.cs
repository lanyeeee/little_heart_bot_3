namespace little_heart_bot_3.Services.Implements.Bot;

public class BotEmailService : EmailService
{
    public BotEmailService(ILogger<BotHostedService> logger, IConfiguration configuration) : base(logger, configuration)
    {
    }
}