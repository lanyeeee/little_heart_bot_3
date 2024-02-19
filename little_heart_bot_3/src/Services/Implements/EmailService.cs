using System.Net;
using System.Net.Mail;

namespace little_heart_bot_3.Services.Implements;

public abstract class EmailService : IEmailService
{
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;

    protected EmailService(ILogger logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task SendEmailAsync(string title, string content)
    {
        try
        {
            string? from = _configuration.GetValue<string>("Email:from");
            string? to = _configuration.GetValue<string>("Email:to");
            string? auth = _configuration.GetValue<string>("Email:auth");
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to) || string.IsNullOrEmpty(auth))
            {
                _logger.LogDebug("邮件配置不完整，不发送邮件");
                return;
            }

            MailMessage email = new MailMessage();
            email.From = new MailAddress(from);
            email.To.Add(to);
            email.Subject = title;
            email.Body = content;

            var smtpClient = new SmtpClient("smtp.qq.com", 25);
            smtpClient.Credentials = new NetworkCredential(from, auth);
            smtpClient.EnableSsl = true;

            await smtpClient.SendMailAsync(email);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "发送邮件失败");
        }
    }
}