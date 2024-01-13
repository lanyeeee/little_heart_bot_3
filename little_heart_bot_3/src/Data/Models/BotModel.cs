namespace little_heart_bot_3.Data.Models;

public class BotModel
{
    public long Uid { get; set; }
    public string Cookie { get; set; } = string.Empty;
    public string Csrf { get; set; } = string.Empty;
    public string DevId { get; set; } = string.Empty;
    public AppStatus AppStatus { get; set; }
    public ReceiveStatus ReceiveStatus { get; set; }
    public SendStatus SendStatus { get; set; }

    public static BotModel LoadFromConfiguration(IConfiguration configuration)
    {
        return new BotModel
        {
            Uid = configuration.GetValue<long>("Bot:uid"),
            Cookie = configuration.GetValue<string>("Bot:cookie")!,
            Csrf = configuration.GetValue<string>("Bot:csrf")!,
            DevId = configuration.GetValue<string>("Bot:dev_id")!,
            AppStatus = configuration.GetValue<AppStatus>("Bot:app_status"),
            ReceiveStatus = configuration.GetValue<ReceiveStatus>("Bot:receive_status"),
            SendStatus = configuration.GetValue<SendStatus>("Bot:send_status")
        };
    }
}

public enum AppStatus
{
    Normal = 0,
    Cooling = -1
}

public enum ReceiveStatus
{
    Normal = 0,
    Cooling = -1
}

public enum SendStatus
{
    Normal = 0,
    Cooling = -1,
    Forbidden = -2
}