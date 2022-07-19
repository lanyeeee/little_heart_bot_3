using little_heart_bot_3.others;

namespace little_heart_bot_3.main;

public class Bot
{
    private static Bot? _instance;
    public static Bot Instance => _instance ?? new Bot();

    private readonly Logger _logger;
    private bool _talking = true;
    private int _talkNum;
    private long _midnight; //今天0点的分钟时间戳

    private Bot()
    {
        _instance = this;
        DateTimeOffset today = DateTime.Today;
        _midnight = today.ToUnixTimeSeconds();
        _logger = new Logger("bot");
    }

    private async Task<bool> IsNewDay()
    {
        if (DateTimeOffset.Now.ToUnixTimeSeconds() - _midnight < 24 * 60 * 60 + 10 * 60)
            return false;

        //新的一天要把一些数据重置
        DateTimeOffset today = DateTime.Today;
        _midnight = today.ToUnixTimeSeconds();

        await Globals.MessageRepository.NewDay();
        await Globals.TargetRepository.NewDay();
        await Globals.UserRepository.NewDay();

        return true;
    }

    public async Task Main()
    {
    }
}