using little_heart_bot_3.Models;

namespace little_heart_bot_3.Services.Implements;

public partial class MessageService
{
    public Task NewDayAsync(CancellationToken cancellationToken = default)
    {
        return _messageRepository.NewDayAsync(cancellationToken);
    }

    public Task<List<MessageModel>> GetMessagesByUid(string? uid, CancellationToken cancellationToken = default)
    {
        return _messageRepository.GetMessagesByUidAsync(uid, cancellationToken);
    }

    public Task<MessageModel?> GetMessagesByUidAndTargetUid(string? uid, string? targetUid,
        CancellationToken cancellationToken = default)
    {
        return _messageRepository.GetMessagesByUidAndTargetUidAsync(uid, targetUid, cancellationToken);
    }

    public Task<List<MessageModel>> GetUncompletedMessagesByUid(string? uid, CancellationToken cancellationToken = default)
    {
        return _messageRepository.GetUncompletedMessagesByUidAsync(uid, cancellationToken);
    }

    public Task SetCodeAndResponseAsync(int? code, string? response, int id, CancellationToken cancellationToken = default)
    {
        return _messageRepository.SetCodeAndResponseAsync(code, response, id, cancellationToken);
    }

    public Task SetCodeAndResponseByUidAndTargetUid(int? code, string? response, string? uid, string? targetUid,
        CancellationToken cancellationToken = default)
    {
        return _messageRepository.SetCodeAndResponseByUidAndTargetUidAsync(code, response, uid, targetUid,
            cancellationToken);
    }

    public Task SetCompletedAsync(int completed, int id, CancellationToken cancellationToken = default)
    {
        return _messageRepository.SetCompletedAsync(completed, id, cancellationToken);
    }

    public Task SetCompletedByUidAndTargetUid(int completed, string uid, string targetUid,
        CancellationToken cancellationToken = default)
    {
        return _messageRepository.SetCompletedByUidAndTargetUidAsync(completed, uid, targetUid, cancellationToken);
    }

    public Task DeleteByUid(string? uid, CancellationToken cancellationToken = default)
    {
        return _messageRepository.DeleteByUidAsync(uid, cancellationToken);
    }

    public Task DeleteByUidAndTargetUid(string? uid, string? targetUid, CancellationToken cancellationToken = default)
    {
        return _messageRepository.DeleteByUidAndTargetUidAsync(uid, targetUid, cancellationToken);
    }

    public Task<bool> CheckExistByUidAndTargetUid(string uid, string targetUid, CancellationToken cancellationToken = default)
    {
        return _messageRepository.CheckExistByUidAndTargetUidAsync(uid, targetUid, cancellationToken);
    }

    public Task Insert(MessageModel message, CancellationToken cancellationToken = default)
    {
        return _messageRepository.InsertAsync(message, cancellationToken);
    }

    public Task SetContentByUidAndTargetUid(string? content, string? uid, string? targetUid,
        CancellationToken cancellationToken = default)
    {
        return _messageRepository.SetContentByUidAndTargetUidAsync(content, uid, targetUid, cancellationToken);
    }

    public Task<int> GetMessageNum(string? uid, CancellationToken cancellationToken = default)
    {
        return _messageRepository.GetMessageNumAsync(uid, cancellationToken);
    }
}