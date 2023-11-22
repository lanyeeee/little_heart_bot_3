using little_heart_bot_3.Models;

namespace little_heart_bot_3.Services.Implements;

public partial class TargetService
{
    public Task NewDayAsync(CancellationToken cancellationToken = default)
    {
        return _targetRepository.NewDayAsync(cancellationToken);
    }

    public Task<List<TargetModel>> GetUncompletedTargetsByUidAsync(string? uid, CancellationToken cancellationToken = default)
    {
        return _targetRepository.GetUncompletedTargetsByUidAsync(uid, cancellationToken);
    }

    public Task SetExpAsync(int exp, int id, CancellationToken cancellationToken = default)
    {
        return _targetRepository.SetExpAsync(exp, id, cancellationToken);
    }

    public Task SetCompletedAsync(int completed, int id, CancellationToken cancellationToken = default)
    {
        return _targetRepository.SetCompletedAsync(completed, id, cancellationToken);
    }

    public Task SetWatchedSecondsAsync(int watchedSeconds, int id, CancellationToken cancellationToken = default)
    {
        return _targetRepository.SetWatchedSecondsAsync(watchedSeconds, id, cancellationToken);
    }

    public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        return _targetRepository.DeleteAsync(id, cancellationToken);
    }

    public Task DeleteByUidAsync(string? uid, CancellationToken cancellationToken = default)
    {
        return _targetRepository.DeleteByUidAsync(uid, cancellationToken);
    }

    public Task DeleteByUidAndTargetUidAsync(string? uid, string? targetUid, CancellationToken cancellationToken = default)
    {
        return _targetRepository.DeleteByUidAndTargetUidAsync(uid, targetUid, cancellationToken);
    }

    public Task<int> GetTargetNumAsync(string? uid, CancellationToken cancellationToken = default)
    {
        return _targetRepository.GetTargetNumAsync(uid, cancellationToken);
    }

    public Task<bool> CheckExistByUidAndTargetUidAsync(string? uid, string? targetUid,
        CancellationToken cancellationToken = default)
    {
        return _targetRepository.CheckExistByUidAndTargetUidAsync(uid, targetUid, cancellationToken);
    }

    public Task SetTargetNameAndRoomIdByUidAndTargetUidAsync(string? targetName, string? roomId, string uid,
        string targetUid, CancellationToken cancellationToken = default)
    {
        return _targetRepository.SetTargetNameAndRoomIdByUidAndTargetUidAsync(targetName, roomId, uid, targetUid,
            cancellationToken);
    }

    public Task InsertAsync(TargetModel target, CancellationToken cancellationToken = default)
    {
        return _targetRepository.InsertAsync(target, cancellationToken);
    }

    public Task<List<TargetModel>> GetTargetsByUidAsync(string? uid, CancellationToken cancellationToken = default)
    {
        return _targetRepository.GetTargetsByUidAsync(uid, cancellationToken);
    }

    public Task<TargetModel?> GetTargetsByUidAndTargetUidAsync(string? uid, string? targetUid,
        CancellationToken cancellationToken = default)
    {
        return _targetRepository.GetTargetsByUidAndTargetUidAsync(uid, targetUid, cancellationToken);
    }
}