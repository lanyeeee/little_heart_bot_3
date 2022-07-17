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


    public async Task Main()
    {
    }
}