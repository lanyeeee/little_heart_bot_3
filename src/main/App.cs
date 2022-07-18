using little_heart_bot_3.entity;
using little_heart_bot_3.others;

namespace little_heart_bot_3.main;

public class App
{
    private static App? _instance;
    public static App Instance => _instance ?? new App();

    private readonly Logger _logger;
    private List<UserEntity> _users;

    private App()
    {
        _instance = this;
        _logger = new Logger("app");
    }

    public async Task SendMessage()
    {
        foreach (var user in _users)
        {
            List<MessageEntity> messages = await Globals.MessageRepository.GetMessagesByUid(user.Uid);
            foreach (var message in messages)
            {
                await message.Send(user.Cookie, user.Csrf, _logger);
            }
        }
    }


    public async Task Main()
    {
        _users = await Globals.UserRepository.GetIncompletedUsers(20);
    }
}