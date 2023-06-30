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

    public async Task<List<UserEntity>> GetUncompletedUsers(int num)
    {
        string sql = $"select * from user_table where completed = 0 and cookie_status = 1 limit {num}";
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        var result = await conn.QueryAsync<UserEntity>(sql);
        return result.ToList();
    }

    public async Task<List<UserEntity>> GetUnverifiedUsers()
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        var result = await conn.QueryAsync<UserEntity>("select * from user_table where cookie_status = 0");
        return result.ToList();
    }

    public async Task MarkCookieError(string? uid)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync($"update user_table set cookie_status = -1 where uid = {uid}");
    }

    public async Task MarkCookieValid(string? uid)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync($"update user_table set cookie_status = 1 where uid = {uid}");
    }

    public async Task SetCompleted(int completed, string? uid)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync($"update user_table set completed = {completed} where uid = {uid}");
    }

    public async Task<UserEntity?> Get(string? uid)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        string sql = $"select * from user_table where uid = {uid}";
        UserEntity? user = await conn.QueryFirstOrDefaultAsync<UserEntity?>(sql);

        if (user == null) return null;

        user.Targets = await Globals.TargetRepository.GetTargetsByUid(uid);
        user.Messages = await Globals.MessageRepository.GetMessagesByUid(uid);

        return user;
    }

    public async Task<List<UserEntity>> GetAll()
    {
        List<UserEntity> users = new();
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        var uidResult = await conn.QueryAsync<string>("select uid from user_table where 1");
        foreach (string uid in uidResult)
        {
            UserEntity? user = await Get(uid);
            if (user == null) continue;
            users.Add(user);
        }

        return users;
    }

    public async Task SetReadTimestamp(string readTimestamp, string uid)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync($"update user_table set read_timestamp = {readTimestamp} where uid = {uid}");
    }

    public async Task Insert(UserEntity userEntity)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        string sql =
            "insert into user_table(uid, cookie, csrf, completed, cookie_status, config_num, read_timestamp, config_timestamp) values(@Uid, @Cookie, @Csrf, @Completed, @CookieStatus, @ConfigNum, @ReadTimestamp, @ConfigTimestamp)";
        await conn.ExecuteAsync(sql, userEntity);
    }

    public async Task Delete(string? uid)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync($"delete from user_table where uid = {uid}");
    }

    public async Task Update(UserEntity userEntity)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        string sql =
            "update user_table set cookie = @Cookie, csrf = @Csrf, completed = @Completed, cookie_status = @CookieStatus, config_num = @ConfigNum, read_timestamp = @ReadTimestamp, config_timestamp = @ConfigTimestamp where uid = @Uid";
        await conn.ExecuteAsync(sql, userEntity);
    }
}