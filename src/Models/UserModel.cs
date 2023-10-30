namespace little_heart_bot_3.Models;

public class UserModel
{
    public string? Uid { get; set; }
    public string? Cookie { get; set; }
    public string? Csrf { get; set; }
    public int Completed { get; set; }
    public int CookieStatus { get; set; }
    public int ConfigNum { get; set; }
    public int TargetNum { get; set; }
    public string? ReadTimestamp { get; set; }
    public string? ConfigTimestamp { get; set; }

    //一对多
    public List<MessageModel>? Messages { get; set; }

    //一对多
    public List<TargetModel>? Targets { get; set; }
}