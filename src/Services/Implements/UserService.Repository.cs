using little_heart_bot_3.Models;

namespace little_heart_bot_3.Services.Implements;

public partial class UserService
{
    public Task NewDay(CancellationToken cancellationToken = default)
    {
        return _userRepository.NewDayAsync(cancellationToken);
    }

    public Task<List<UserModel>> GetUncompletedUsersAsync(int num, CancellationToken cancellationToken = default)
    {
        return _userRepository.GetUncompletedUsersAsync(num, cancellationToken);
    }

    public Task<List<UserModel>> GetUnverifiedUsersAsync(CancellationToken cancellationToken = default)
    {
        return _userRepository.GetUnverifiedUsersAsync(cancellationToken);
    }

    public Task MarkCookieError(string? uid, CancellationToken cancellationToken = default)
    {
        return _userRepository.MarkCookieErrorAsync(uid, cancellationToken);
    }

    public Task MarkCookieValid(string? uid, CancellationToken cancellationToken = default)
    {
        return _userRepository.MarkCookieValidAsync(uid, cancellationToken);
    }

    public Task SetCompleted(int completed, string? uid, CancellationToken cancellationToken = default)
    {
        return _userRepository.SetCompletedAsync(completed, uid, cancellationToken);
    }

    public Task<UserModel?> Get(string? uid, CancellationToken cancellationToken = default)
    {
        return _userRepository.GetAsync(uid, cancellationToken);
    }

    public Task<List<UserModel>> GetAll(CancellationToken cancellationToken = default)
    {
        return _userRepository.GetAllAsync(cancellationToken);
    }

    public Task SetReadTimestamp(string readTimestamp, string uid, CancellationToken cancellationToken = default)
    {
        return _userRepository.SetReadTimestampAsync(readTimestamp, uid, cancellationToken);
    }

    public Task Insert(UserModel user, CancellationToken cancellationToken = default)
    {
        return _userRepository.InsertAsync(user, cancellationToken);
    }

    public Task Delete(string? uid, CancellationToken cancellationToken = default)
    {
        return _userRepository.DeleteAsync(uid, cancellationToken);
    }

    public Task Update(UserModel user, CancellationToken cancellationToken = default)
    {
        return _userRepository.UpdateAsync(user, cancellationToken);
    }
}