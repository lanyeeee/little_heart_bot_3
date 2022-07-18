using Dapper;
using little_heart_bot_3.entity;
using little_heart_bot_3.others;
using MySqlConnector;

namespace little_heart_bot_3.repository;

public class UserRepository
{
    public async Task NewDay()
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync("update user_table set completed = 0, config_num = 0 where 1");
    }

    public async Task<List<UserEntity>> GetIncompletedUsers(int num)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        var parameters = new { Num = num };
        var result = await conn.QueryAsync<UserEntity>("select * from user_table limit @Num", parameters);
        return result.ToList();
    }

    public async Task MarkCookieError(string? uid)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync($"update user_table set cookie_status = -1 where uid = {uid}");
    }
}