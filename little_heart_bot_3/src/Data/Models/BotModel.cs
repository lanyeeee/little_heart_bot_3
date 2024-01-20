namespace little_heart_bot_3.Data.Models;

public class BotModel
{
    public long Uid { get; set; }
    public string Cookie { get; set; } = string.Empty;
    public string Csrf { get; set; } = string.Empty;
    public string DevId { get; set; } = string.Empty;

    public static BotModel LoadFromConfiguration(IConfiguration configuration)
    {
        return new BotModel
        {
            Uid = configuration.GetValue<long>("Bot:uid"),
            Cookie = configuration.GetValue<string>("Bot:cookie")!,
            Csrf = configuration.GetValue<string>("Bot:csrf")!,
            DevId = configuration.GetValue<string>("Bot:dev_id")!,
        };
    }
}