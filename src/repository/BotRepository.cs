using Dapper;
using little_heart_bot_3.entity;
using little_heart_bot_3.others;
using MySqlConnector;

namespace little_heart_bot_3.repository;

public class BotRepository
{
    public BotEntity? GetBot()
    {
        using var conn = new MySqlConnection(Globals.ConnectionString);
        return conn.QueryFirstOrDefault<BotEntity?>("select * from bot_table");
    }
}