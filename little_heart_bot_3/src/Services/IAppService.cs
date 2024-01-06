using little_heart_bot_3.Data.Models;

namespace little_heart_bot_3.Services;

public interface IAppService
{
    Task VerifyCookiesAsync(CancellationToken cancellationToken = default);
    Task SendMessageAsync(List<UserModel> users, CancellationToken cancellationToken = default);
    Task WatchLiveAsync(List<UserModel> users, CancellationToken cancellationToken = default);
}