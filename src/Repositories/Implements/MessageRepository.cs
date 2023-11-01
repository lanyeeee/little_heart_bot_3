using Dapper;
using little_heart_bot_3.Models;
using little_heart_bot_3.Others;
using MySqlConnector;

namespace little_heart_bot_3.Repositories.Implements;

public class MessageRepository : IMessageRepository
{
    public async Task NewDayAsync(CancellationToken cancellationToken = default)
    {
        var command = new CommandDefinition("update message_table set code = 0, response = null, completed = 0 where 1",
            cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(command);
    }

    public async Task<List<MessageModel>> GetMessagesByUidAsync(string? uid,
        CancellationToken cancellationToken = default)
    {
        var command = new CommandDefinition($"select * from message_table where uid = {uid}",
            cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        var result = await conn.QueryAsync<MessageModel>(command);
        return result.ToList();
    }

    public async Task<MessageModel?> GetMessagesByUidAndTargetUidAsync(string? uid, string? targetUid,
        CancellationToken cancellationToken = default)
    {
        string sql = $"select * from message_table where uid = {uid} and target_uid = @TargetUid";
        var parameters = new { TargetUid = targetUid };
        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        var result = await conn.QuerySingleOrDefaultAsync<MessageModel?>(command);
        return result;
    }

    public async Task<List<MessageModel>> GetUncompletedMessagesByUidAsync(string? uid,
        CancellationToken cancellationToken = default)
    {
        string sql = $"select * from message_table where completed = 0 and uid = {uid}";
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        var result = await conn.QueryAsync<MessageModel>(command);
        return result.ToList();
    }

    public async Task MarkCookieErrorAsync(int? code, string? response, string? uid,
        CancellationToken cancellationToken = default)
    {
        string sql = "update message_table set code = @Code, response = @Response where uid = @Uid";
        var parameters = new { Code = code, Response = response, Uid = uid };
        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(command);
    }

    public async Task SetCodeAndResponseAsync(int? code, string? response, int id,
        CancellationToken cancellationToken = default)
    {
        string sql = "update message_table set code = @Code, response = @Response where id = @Id";
        var parameters = new { Code = code, Response = response, Id = id };
        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(command);
    }

    public async Task SetCodeAndResponseByUidAndTargetUidAsync(int? code, string? response, string? uid,
        string? targetUid, CancellationToken cancellationToken = default)
    {
        string sql =
            "update message_table set code = @Code, response = @Response where uid = @Uid and target_uid = @TargetUid";
        var parameters = new { Code = code, Response = response, Uid = uid, TargetUid = targetUid };
        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(command);
    }

    public async Task SetCompletedAsync(int completed, int id, CancellationToken cancellationToken = default)
    {
        var command = new CommandDefinition($"update message_table set completed = {completed} where id = {id}",
            cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(command);
    }

    public async Task SetCompletedByUidAndTargetUidAsync(int completed, string uid, string targetUid,
        CancellationToken cancellationToken = default)
    {
        string sql = "update message_table set completed = @Completed where uid = @Uid and target_uid = @TargetUid";
        var parameters = new { Completed = completed, Uid = uid, TargetUid = targetUid };
        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(command);
    }

    public async Task DeleteByUidAsync(string? uid, CancellationToken cancellationToken = default)
    {
        var command =
            new CommandDefinition($"delete from message_table where uid = {uid}", cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(command);
    }

    public async Task DeleteByUidAndTargetUidAsync(string? uid, string? targetUid,
        CancellationToken cancellationToken = default)
    {
        string sql = "delete from message_table where uid = @Uid and target_uid = @TargetUid";
        var parameters = new { Uid = uid, TargetUid = targetUid };
        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(command);
    }

    public async Task<bool> CheckExistByUidAndTargetUidAsync(string uid, string targetUid,
        CancellationToken cancellationToken = default)
    {
        string sql = "select * from message_table where uid = @Uid and target_uid = @TargetUid";
        var parameters = new { Uid = uid, TargetUid = targetUid };
        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        var result = await conn.QueryAsync<TargetModel>(command);
        return result.ToList().Count != 0;
    }

    public async Task InsertAsync(MessageModel messageModel, CancellationToken cancellationToken = default)
    {
        string sql =
            "insert into message_table(uid, target_uid, target_name, room_id, content, code, response, completed) values(@Uid, @TargetUid, @TargetName, @RoomId, @Content, @Code, @response, @Completed)";
        var command = new CommandDefinition(sql, messageModel, cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(command);
    }

    public async Task SetContentByUidAndTargetUidAsync(string? content, string? uid, string? targetUid,
        CancellationToken cancellationToken = default)
    {
        string sql = "update message_table set content = @Content where uid = @Uid and target_uid = @TargetUid";
        var parameters = new { Content = content, Uid = uid, TargetUid = targetUid };
        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(command);
    }

    public async Task<int> GetMessageNumAsync(string? uid, CancellationToken cancellationToken = default)
    {
        var command = new CommandDefinition($"select * from message_table where uid = {uid}",
            cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        var result = await conn.QueryAsync<TargetModel>(command);
        return result.ToList().Count;
    }
}