using little_heart_bot_3.Models;

namespace little_heart_bot_3.Repositories;

public interface IUserRepository
{
    public Task NewDayAsync(CancellationToken cancellationToken = default);

    public Task<List<UserModel>> GetUncompletedUsersAsync(int num, CancellationToken cancellationToken = default);

    public Task<List<UserModel>> GetUnverifiedUsersAsync(CancellationToken cancellationToken = default);

    public Task MarkCookieErrorAsync(string? uid, CancellationToken cancellationToken = default);

    public Task MarkCookieValidAsync(string? uid, CancellationToken cancellationToken = default);

    public Task SetCompletedAsync(int completed, string? uid, CancellationToken cancellationToken = default);

    public Task<UserModel?> GetAsync(string? uid, CancellationToken cancellationToken = default);

    public Task<List<UserModel>> GetAllAsync(CancellationToken cancellationToken = default);

    public Task SetReadTimestampAsync(string readTimestamp, string uid, CancellationToken cancellationToken = default);

    public Task InsertAsync(UserModel userModel, CancellationToken cancellationToken = default);

    public Task DeleteAsync(string? uid, CancellationToken cancellationToken = default);

    public Task UpdateAsync(UserModel userModel, CancellationToken cancellationToken = default);
}