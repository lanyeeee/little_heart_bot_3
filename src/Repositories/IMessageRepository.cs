using little_heart_bot_3.Models;

namespace little_heart_bot_3.Repositories;

public interface IMessageRepository
{
    public Task NewDayAsync(CancellationToken cancellationToken = default);

    public Task<List<MessageModel>> GetMessagesByUidAsync(string? uid, CancellationToken cancellationToken = default);

    public Task<MessageModel?> GetMessagesByUidAndTargetUidAsync(string? uid, string? targetUid,
        CancellationToken cancellationToken = default);

    public Task<List<MessageModel>> GetUncompletedMessagesByUidAsync(string? uid,
        CancellationToken cancellationToken = default);

    public Task MarkCookieErrorAsync(int? code, string? response, string? uid,
        CancellationToken cancellationToken = default);

    public Task SetCodeAndResponseAsync(int? code, string? response, int id,
        CancellationToken cancellationToken = default);

    public Task SetCodeAndResponseByUidAndTargetUidAsync(int? code, string? response, string? uid, string? targetUid,
        CancellationToken cancellationToken = default);

    public Task SetCompletedAsync(int completed, int id, CancellationToken cancellationToken = default);

    public Task SetCompletedByUidAndTargetUidAsync(int completed, string uid, string targetUid,
        CancellationToken cancellationToken = default);

    public Task DeleteByUidAsync(string? uid, CancellationToken cancellationToken = default);

    public Task DeleteByUidAndTargetUidAsync(string? uid, string? targetUid,
        CancellationToken cancellationToken = default);

    public Task<bool> CheckExistByUidAndTargetUidAsync(string uid, string targetUid,
        CancellationToken cancellationToken = default);

    public Task InsertAsync(MessageModel messageModel, CancellationToken cancellationToken = default);

    public Task SetContentByUidAndTargetUidAsync(string? content, string? uid, string? targetUid,
        CancellationToken cancellationToken = default);

    public Task<int> GetMessageNumAsync(string? uid, CancellationToken cancellationToken = default);
}