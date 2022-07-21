using Dapper;
using little_heart_bot_3.entity;
using little_heart_bot_3.others;
using MySqlConnector;

namespace little_heart_bot_3.repository;

public class TargetRepository
{
    public async Task NewDay()
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync("update target_table set exp = 0,watched_seconds = 0, completed = 0 where 1");
    }

    public async Task<List<TargetEntity>> GetUncompletedTargetsByUid(string? uid)
    {
        string sql = $"select * from target_table where completed = 0 and uid = {uid}";
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        var result = await conn.QueryAsync<TargetEntity>(sql);
        return result.ToList();
    }

    public async Task SetExp(int exp, int id)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync($"update target_table set exp = {exp} where id = {id}");
    }

    public async Task SetCompleted(int completed, int id)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync($"update target_table set completed = {completed} where id = {id}");
    }

    public async Task SetWatchedSeconds(int watchedSeconds, int id)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync($"update target_table set watched_seconds = {watchedSeconds} where id = {id}");
    }

    public async Task Delete(int id)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync($"delete from target_table where id = {id}");
    }

    public async Task DeleteByUid(string? uid)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync($"delete from target_table where uid = {uid}");
    }

    public async Task<int> GetTargetNum(string? uid)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        var result = await conn.QueryAsync<TargetEntity>($"select * from target_table where uid = {uid}");
        return result.ToList().Count;
    }

    public async Task<bool> CheckExistByUidAndTargetUid(string uid, string targetUid)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        string sql = $"select * from target_table where uid = {uid} and target_uid = {targetUid}";
        var result = await conn.QueryAsync<TargetEntity>(sql);
        return result.ToList().Count != 0;
    }

    public async Task SetTargetNameAndRoomIdByUidAndTargetUid(string? targetName, string? roomId, string uid,
        string targetUid)
    {
        string sql =
            $"update target_table set target_name = '{targetName}', room_id = {roomId} where uid = {uid} and target_uid = {targetUid}";

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(sql);
    }

    public async Task Insert(TargetEntity targetEntity)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        string sql =
            "insert into target_table(uid, target_uid, target_name, room_id, exp, watched_seconds, completed) values(@Uid, @TargetUid, @TargetName, @RoomId, @Exp, @WatchedSeconds, @Completed)";
        await conn.ExecuteAsync(sql, targetEntity);
    }
}