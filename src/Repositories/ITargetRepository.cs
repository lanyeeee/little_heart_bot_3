using little_heart_bot_3.Models;


namespace little_heart_bot_3.Repositories;

public interface ITargetRepository
{
    public Task NewDayAsync(CancellationToken cancellationToken = default);

    public Task<List<TargetModel>> GetUncompletedTargetsByUidAsync(string? uid,
        CancellationToken cancellationToken = default);

    public Task SetExpAsync(int exp, int id, CancellationToken cancellationToken = default);

    public Task SetCompletedAsync(int completed, int id, CancellationToken cancellationToken = default);

    public Task SetWatchedSecondsAsync(int watchedSeconds, int id, CancellationToken cancellationToken = default);

    public Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    public Task DeleteByUidAsync(string? uid, CancellationToken cancellationToken = default);

    public Task DeleteByUidAndTargetUidAsync(string? uid, string? targetUid,
        CancellationToken cancellationToken = default);

    public Task<int> GetTargetNumAsync(string? uid, CancellationToken cancellationToken = default);

    public Task<bool> CheckExistByUidAndTargetUidAsync(string? uid, string? targetUid,
        CancellationToken cancellationToken = default);

    public Task SetTargetNameAndRoomIdByUidAndTargetUidAsync(string? targetName, string? roomId, string uid,
        string targetUid, CancellationToken cancellationToken = default);

    public Task InsertAsync(TargetModel targetModel, CancellationToken cancellationToken = default);

    public Task<List<TargetModel>> GetTargetsByUidAsync(string? uid, CancellationToken cancellationToken = default);

    public Task<TargetModel?> GetTargetsByUidAndTargetUidAsync(string? uid, string? targetUid,
        CancellationToken cancellationToken = default);
}