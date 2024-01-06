using little_heart_bot_3.Data.Models;

namespace little_heart_bot_3.Services;

public interface IAppService
{
    Task VerifyCookiesAsync(CancellationToken cancellationToken);
    Task SendMessageAsync(List<UserModel> users, CancellationToken cancellationToken);
    Task WatchLiveAsync(List<UserModel> users, CancellationToken cancellationToken);
}