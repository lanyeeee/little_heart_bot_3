using Dapper;
using little_heart_bot_3.entity;
using little_heart_bot_3.others;
using MySqlConnector;

namespace little_heart_bot_3.repository;

public class BotRepository
{
    public async Task<BotEntity> GetBot()
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        return await conn.QueryFirstAsync("select * from bot_table");
    }
}