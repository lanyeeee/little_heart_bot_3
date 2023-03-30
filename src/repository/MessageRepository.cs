using Dapper;
using little_heart_bot_3.entity;
using little_heart_bot_3.others;
using MySqlConnector;

namespace little_heart_bot_3.repository;

public class MessageRepository
{
    public async Task NewDay()
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync("update message_table set code = 0, response = null, completed = 0 where 1");
    }

    public async Task<List<MessageEntity>> GetMessagesByUid(string? uid)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        var result = await conn.QueryAsync<MessageEntity>($"select * from message_table where uid = {uid}");
        return result.ToList();
    }

    public async Task<MessageEntity?> GetMessagesByUidAndTargetUid(string? uid, string? targetUid)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        string sql = $"select * from message_table where uid = {uid} and target_uid = @TargetUid";
        var parameters = new { TargetUid = targetUid };
        var result = await conn.QuerySingleOrDefaultAsync<MessageEntity?>(sql, parameters);
        return result;
    }

    public async Task<List<MessageEntity>> GetUncompletedMessagesByUid(string? uid)
    {
        string sql = $"select * from message_table where completed = 0 and uid = {uid}";
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        var result = await conn.QueryAsync<MessageEntity>(sql);
        return result.ToList();
    }

    public async Task MarkCookieError(int? code, string? response, string? uid)
    {
        string sql = "update message_table set code = @Code, response = @Response where uid = @Uid";
        var parameters = new { Code = code, Response = response, Uid = uid };
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(sql, parameters);
    }

    public async Task SetCodeAndResponse(int? code, string? response, int id)
    {
        string sql = "update message_table set code = @Code, response = @Response where id = @Id";
        var parameters = new { Code = code, Response = response, Id = id };

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(sql, parameters);
    }

    public async Task SetCodeAndResponseByUidAndTargetUid(int? code, string? response, string? uid, string? targetUid)
    {
        string sql =
            "update message_table set code = @Code, response = @Response where uid = @Uid and target_uid = @TargetUid";
        var parameters = new { Code = code, Response = response, Uid = uid, TargetUid = targetUid };

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(sql, parameters);
    }

    public async Task SetCompleted(int completed, int id)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync($"update message_table set completed = {completed} where id = {id}");
    }

    public async Task SetCompletedByUidAndTargetUid(int completed, string uid, string targetUid)
    {
        string sql = "update message_table set completed = @Completed where uid = @Uid and target_uid = @TargetUid";
        var parameters = new { Completed = completed, Uid = uid, TargetUid = targetUid };

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(sql, parameters);
    }

    public async Task DeleteByUid(string? uid)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync($"delete from message_table where uid = {uid}");
    }

    public async Task DeleteByUidAndTargetUid(string? uid, string? targetUid)
    {
        string sql = "delete from message_table where uid = @Uid and target_uid = @TargetUid";
        var parameters = new { Uid = uid, TargetUid = targetUid };

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(sql, parameters);
    }

    public async Task<bool> CheckExistByUidAndTargetUid(string uid, string targetUid)
    {
        string sql = "select * from message_table where uid = @Uid and target_uid = @TargetUid";
        var parameters = new { Uid = uid, TargetUid = targetUid };

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        var result = await conn.QueryAsync<TargetEntity>(sql, parameters);
        return result.ToList().Count != 0;
    }

    public async Task Insert(MessageEntity messageEntity)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        string sql =
            "insert into message_table(uid, target_uid, target_name, room_id, content, code, response, completed) values(@Uid, @TargetUid, @TargetName, @RoomId, @Content, @Code, @response, @Completed)";
        await conn.ExecuteAsync(sql, messageEntity);
    }

    public async Task SetContentByUidAndTargetUid(string? content, string? uid, string? targetUid)
    {
        string sql = "update message_table set content = @Content where uid = @Uid and target_uid = @TargetUid";
        var parameters = new { Content = content, Uid = uid, TargetUid = targetUid };

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(sql, parameters);
    }

    public async Task<int> GetMessageNum(string? uid)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        var result = await conn.QueryAsync<TargetEntity>($"select * from message_table where uid = {uid}");
        return result.ToList().Count;
    }
}