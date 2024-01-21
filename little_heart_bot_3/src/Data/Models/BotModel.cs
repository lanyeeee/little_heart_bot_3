using little_heart_bot_3.Others;

namespace little_heart_bot_3.Data.Models;

public class BotModel
{
    public required long Uid { get; init; }
    public required string Cookie { get; init; }
    public required string Csrf { get; init; }
    public required string DevId { get; init; }

    private BotModel()
    {
    }

    public static BotModel? LoadFromConfiguration(IConfiguration configuration)
    {
        try
        {
            var cookie = configuration.GetValue<string>("Bot:cookie")!;
            return new BotModel
            {
                Uid = configuration.GetValue<long>("Bot:uid"),
                Cookie = cookie,
                Csrf = Globals.GetCsrf(cookie),
                DevId = configuration.GetValue<string>("Bot:dev_id")!,
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}