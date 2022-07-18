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
        await conn.ExecuteAsync("update message_table set code = 0, completed = 0 where 1");
    }

    public async Task<List<MessageEntity>> GetMessagesByUid(string? uid)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        var result = await conn.QueryAsync<MessageEntity>($"select * from message_table where uid = {uid}");
        return result.ToList();
    }

    public async Task MarkCookieError(string? uid, int? code, string? response)
    {
        string sql = "update message_table set code = @Code, response = @Response where uid = @Uid";
        var parameters = new { Code = code, Response = response, Uid = uid };
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(sql, parameters);
    }

    public async Task Save(MessageEntity messageEntity)
    {
        string sql =
            "update message_table set uid = @Uid, target_uid = @TargetUid, target_name = @TargetName, room_id = @RoomId, content = @Content, code = @Code, response = @Response, completed = @Completed where id = @Id";
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(sql, messageEntity);
    }
}