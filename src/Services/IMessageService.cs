using little_heart_bot_3.Models;

namespace little_heart_bot_3.Services;

public interface IMessageService
{
    public Task SendAsync(MessageModel messageModel, string? cookie, string? csrf,
        CancellationToken cancellationToken = default);

    public Task NewDayAsync(CancellationToken cancellationToken = default);

    public Task ThumbsUpAsync(MessageModel message, string? cookie, string? csrf,
        CancellationToken cancellationToken = default);

    public Task<List<MessageModel>> GetMessagesByUid(string? uid, CancellationToken cancellationToken = default);

    public Task<MessageModel?> GetMessagesByUidAndTargetUid(string? uid, string? targetUid,
        CancellationToken cancellationToken = default);

    public Task<List<MessageModel>> GetUncompletedMessagesByUid(string? uid,
        CancellationToken cancellationToken = default);

    public Task SetCodeAndResponseAsync(int? code, string? response, int id,
        CancellationToken cancellationToken = default);

    public Task SetCodeAndResponseByUidAndTargetUid(int? code, string? response, string? uid, string? targetUid,
        CancellationToken cancellationToken = default);

    public Task SetCompletedAsync(int completed, int id, CancellationToken cancellationToken = default);

    public Task SetCompletedByUidAndTargetUid(int completed, string uid, string targetUid,
        CancellationToken cancellationToken = default);

    public Task DeleteByUid(string? uid, CancellationToken cancellationToken = default);

    public Task DeleteByUidAndTargetUid(string? uid, string? targetUid, CancellationToken cancellationToken = default);

    public Task<bool> CheckExistByUidAndTargetUid(string uid, string targetUid,
        CancellationToken cancellationToken = default);

    public Task Insert(MessageModel messageModel, CancellationToken cancellationToken = default);

    public Task SetContentByUidAndTargetUid(string? content, string? uid, string? targetUid,
        CancellationToken cancellationToken = default);

    public Task<int> GetMessageNum(string? uid, CancellationToken cancellationToken = default);
}