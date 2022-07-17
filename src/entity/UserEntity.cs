namespace little_heart_bot_3.entity;

public class UserEntity
{
    public string Uid { get; set; }
    public string Cookie { get; set; }
    public string Csrf { get; set; }
    public int Completed { get; set; }
    public int CookieStatus { get; set; }
    public int ConfigNum { get; set; }
    public int TargetNum { get; set; }
    public string ReadTimestamp { get; set; }
    public string ConfigTimestamp { get; set; }
}