namespace little_heart_bot_3.Services;

public interface IEmailService
{
    /// <summary>
    /// 发送邮件
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="content">内容</param>
    public Task SendEmailAsync(string title, string content);
}