using little_heart_bot_3.Models;

namespace little_heart_bot_3.Services;

public interface IUserService
{
    public Task SendMessageAsync(UserModel userModel, CancellationToken cancellationToken = default);

    public Task WatchLiveAsync(UserModel userModel, CancellationToken cancellationToken = default);

    public string? GetConfigString(UserModel userModel);

    public string? GetMessageConfigString(UserModel userModel);

    public List<string>? GetMessageConfigStringSplit(UserModel userModel);

    public Task<string?> GetMessageConfigStringAsync(UserModel userModel, string targetUid,
        CancellationToken cancellationToken = default);

    public string? GetTargetConfigString(UserModel userModel);

    public List<string>? GetTargetConfigStringSplit(UserModel userModel);

    public Task<string?> GetTargetConfigStringAsync(UserModel userModel, string targetUid,
        CancellationToken cancellationToken = default);

    public Task NewDay(CancellationToken cancellationToken = default);

    public Task<List<UserModel>> GetUncompletedUsersAsync(int num, CancellationToken cancellationToken = default);

    public Task<List<UserModel>> GetUnverifiedUsersAsync(CancellationToken cancellationToken = default);

    public Task MarkCookieError(string? uid, CancellationToken cancellationToken = default);

    public Task MarkCookieValid(string? uid, CancellationToken cancellationToken = default);

    public Task SetCompleted(int completed, string? uid, CancellationToken cancellationToken = default);

    public Task<UserModel?> Get(string? uid, CancellationToken cancellationToken = default);

    public Task<List<UserModel>> GetAll(CancellationToken cancellationToken = default);

    public Task SetReadTimestamp(string readTimestamp, string uid, CancellationToken cancellationToken = default);

    public Task Insert(UserModel userModel, CancellationToken cancellationToken = default);

    public Task Delete(string? uid, CancellationToken cancellationToken = default);

    public Task Update(UserModel userModel, CancellationToken cancellationToken = default);
}