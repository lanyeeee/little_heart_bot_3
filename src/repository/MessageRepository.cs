using Dapper;
using little_heart_bot_3.others;
using MySqlConnector;

namespace little_heart_bot_3.repository;

public class MessageRepository
{
    public async Task NewDay()
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync("update message_table set status = 0, completed = 0 where 1");
    }
}