using little_heart_bot_3.entity;
using little_heart_bot_3.others;
using Newtonsoft.Json.Linq;

namespace little_heart_bot_3.main;

public class Bot
{
    private static Bot? _instance;
    public static Bot Instance => _instance ?? new Bot();

    private readonly Logger _logger;
    private bool _talking = true;
    private int _talkNum;
    private long _midnight; //今天0点的分钟时间戳
    private BotEntity _botEntity;


    private Bot()
    {
        _instance = this;

        DateTimeOffset today = DateTime.Today;
        _midnight = today.ToUnixTimeSeconds();

        _logger = new Logger("bot");

        _botEntity = Globals.BotRepository.GetBot();
        Globals.AppStatus = _botEntity.AppStatus;
        Globals.SendStatus = _botEntity.SendStatus;
        Globals.ReceiveStatus = _botEntity.ReceiveStatus;
    }

    private async Task CheckNewDay()
    {
        if (DateTimeOffset.Now.ToUnixTimeSeconds() - _midnight < 24 * 60 * 60 + 3 * 60) return;

        //新的一天要把一些数据重置
        DateTimeOffset today = DateTime.Today;
        _midnight = today.ToUnixTimeSeconds();

        await Globals.MessageRepository.NewDay();
        await Globals.TargetRepository.NewDay();
        await Globals.UserRepository.NewDay();
    }

    private async Task BotMain()
    {
        while (true)
        {
            try
            {
                await CheckNewDay();
                Globals.SendStatus = 0;
                Globals.ReceiveStatus = 0;
            }
            catch (ApiException)
            {
                int cd = 15;
                Globals.SendStatus = -1;
                Globals.ReceiveStatus = -1;
                while (cd != 0)
                {
                    await _logger.Log($"请求过于频繁，还需冷却 {cd} 分钟");
                    await Task.Delay(cd * 60 * 1000);
                    cd--;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                await Task.Delay(5000);
            }
        }
    }

    public async Task Main()
    {
        var tasks = new List<Task>
        {
            _botEntity.UpdateSign(_logger), BotMain()
        };
        await Task.WhenAll(tasks);
    }
}