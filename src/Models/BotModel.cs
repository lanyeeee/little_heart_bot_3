namespace little_heart_bot_3.Models;

public class BotModel
{
    public string? Uid { get; set; }
    public string? Cookie { get; set; }
    public string? Csrf { get; set; }
    public string? DevId { get; set; }
    public int AppStatus { get; set; }
    public int ReceiveStatus { get; set; }
    public int SendStatus { get; set; }
}