using Dapper;
using little_heart_bot_3.others;
using MySqlConnector;

namespace little_heart_bot_3.repository;

public class TargetRepository
{
    public async Task NewDay()
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync("update target_table set exp = 0, completed = 0 where 1");
    }
}