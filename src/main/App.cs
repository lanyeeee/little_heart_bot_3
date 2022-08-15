using little_heart_bot_3.entity;
using little_heart_bot_3.others;
using Newtonsoft.Json.Linq;

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

    private async Task VerifyCookies()
    {
        List<UserEntity> users = await Globals.UserRepository.GetUnverifiedUsers();
        foreach (var user in users)
        {
            HttpResponseMessage responseMessage = await Globals.HttpClient.SendAsync(new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri("https://api.bilibili.com/x/web-interface/nav"),
                Headers = { { "Cookie", user.Cookie } }
            });
            await Task.Delay(1000);

            JObject response = JObject.Parse(await responseMessage.Content.ReadAsStringAsync());
            int? code = (int?)response["code"];

            if (code == -412)
            {
                await _logger.Log(response);
                throw new ApiException();
            }

            if (code != 0)
            {
                await _logger.Log(response);
                await _logger.Log($"uid {user.Uid} 提供的cookie错误或已过期");
                await Globals.UserRepository.MarkCookieError(user.Uid);
            }
            else
            {
                await Globals.UserRepository.MarkCookieValid(user.Uid);
            }
        }
    }

    private async Task SendMessage(List<UserEntity> users)
    {
        var tasks = new List<Task>();

        foreach (var user in users)
        {
            tasks.Add(user.SendMessage(_logger));
            await Task.Delay(100);
        }

        // users.ForEach(user => tasks.Add(user.SendMessage(_logger)));
        await Task.WhenAll(tasks);
    }

    private async Task WatchLive(List<UserEntity> users)
    {
        var tasks = new List<Task>();
        foreach (var user in users)
        {
            tasks.Add(user.WatchLive(_logger));
            await Task.Delay(2000);
        }

        // users.ForEach(user => tasks.Add(user.WatchLive(_logger)));
        await Task.WhenAll(tasks);
    }

    public async Task Main()
    {
        while (true)
        {
            try
            {
                await VerifyCookies();
                List<UserEntity> users = await Globals.UserRepository.GetUncompletedUsers(30);
                await SendMessage(users);
                await WatchLive(users);
                Globals.AppStatus = 0;
            }
            catch (ApiException)
            {
                Globals.AppStatus = -1;
                int cd = 15;
                while (cd != 0)
                {
                    await _logger.Log($"请求过于频繁，还需冷却 {cd} 分钟");
                    await Task.Delay(60 * 1000);
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
}