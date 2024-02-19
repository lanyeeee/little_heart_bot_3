namespace little_heart_bot_3.Services.Implements.App;

public class AppEmailService : EmailService
{
    public AppEmailService(ILogger<AppHostedService> logger, IConfiguration configuration) : base(logger, configuration)
    {
    }
}