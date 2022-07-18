using little_heart_bot_3.entity;
using little_heart_bot_3.others;

namespace little_heart_bot_3.main;

public class App
{
    private static App? _instance;
    public static App Instance => _instance ?? new App();

    private readonly Logger _logger;


    private App()
    {
        _instance = this;
        _logger = new Logger("app");
    }

    private async Task SendMessage(List<UserEntity> users)
    {
        var tasks = new List<Task>();
        users.ForEach(user => tasks.Add(user.SendMessage(_logger)));
        await Task.WhenAll(tasks);
    }

    public async Task Main()
    {
        while (true)
        {
            try
            {
                List<UserEntity> users = await Globals.UserRepository.GetUncompletedUsers(20);
                await SendMessage(users);
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
}