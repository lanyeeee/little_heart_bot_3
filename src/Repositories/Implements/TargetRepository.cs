using Dapper;
using little_heart_bot_3.Models;
using little_heart_bot_3.Others;
using MySqlConnector;

namespace little_heart_bot_3.Repositories.Implements;

public class TargetRepository : ITargetRepository
{
    public async Task NewDayAsync(CancellationToken cancellationToken = default)
    {
        var command =
            new CommandDefinition("update target_table set exp = 0,watched_seconds = 0, completed = 0 where 1",
                cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(command);
    }

    public async Task<List<TargetModel>> GetUncompletedTargetsByUidAsync(string? uid,
        CancellationToken cancellationToken = default)
    {
        string sql = $"select * from target_table where completed = 0 and uid = {uid}";
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        var result = await conn.QueryAsync<TargetModel>(command);
        return result.ToList();
    }

    public async Task SetExpAsync(int exp, int id, CancellationToken cancellationToken = default)
    {
        var command = new CommandDefinition($"update target_table set exp = {exp} where id = {id}",
            cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(command);
    }

    public async Task SetCompletedAsync(int completed, int id, CancellationToken cancellationToken = default)
    {
        var command = new CommandDefinition($"update target_table set completed = {completed} where id = {id}",
            cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(command);
    }

    public async Task SetWatchedSecondsAsync(int watchedSeconds, int id, CancellationToken cancellationToken = default)
    {
        var command =
            new CommandDefinition($"update target_table set watched_seconds = {watchedSeconds} where id = {id}",
                cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(command);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var command = new CommandDefinition($"delete from target_table where id = {id}",
            cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(command);
    }

    public async Task DeleteByUidAsync(string? uid, CancellationToken cancellationToken = default)
    {
        var command = new CommandDefinition($"delete from target_table where uid = {uid}",
            cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(command);
    }

    public async Task DeleteByUidAndTargetUidAsync(string? uid, string? targetUid,
        CancellationToken cancellationToken = default)
    {
        string sql = "delete from target_table where uid = @Uid and target_uid = @TargetUid";
        var parameters = new { Uid = uid, TargetUid = targetUid };
        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(command);
    }

    public async Task<int> GetTargetNumAsync(string? uid, CancellationToken cancellationToken = default)
    {
        var command = new CommandDefinition($"select * from target_table where uid = {uid}",
            cancellationToken: cancellationToken);
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        var result = await conn.QueryAsync<TargetModel>(command);
        return result.ToList().Count;
    }

    public async Task<bool> CheckExistByUidAndTargetUidAsync(string? uid, string? targetUid,
        CancellationToken cancellationToken = default)
    {
        string sql = "select * from target_table where uid = @Uid and target_uid = @TargetUid";
        var parameters = new { Uid = uid, TargetUid = targetUid };
        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        var result = await conn.QueryAsync<TargetModel>(command);
        return result.ToList().Count != 0;
    }

    public async Task SetTargetNameAndRoomIdByUidAndTargetUidAsync(string? targetName, string? roomId, string uid,
        string targetUid, CancellationToken cancellationToken = default)
    {
        string sql =
            "update target_table set target_name = @TargetName, room_id = @RoomId where uid = @Uid and target_uid = @TargetUid";
        var parameters = new
        {
            TargetName = targetName,
            RoomId = roomId,
            Uid = uid,
            TargetUid = targetUid
        };
        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(command);
    }

    public async Task InsertAsync(TargetModel targetModel, CancellationToken cancellationToken = default)
    {
        string sql =
            "insert into target_table(uid, target_uid, target_name, room_id, exp, watched_seconds, completed) values(@Uid, @TargetUid, @TargetName, @RoomId, @Exp, @WatchedSeconds, @Completed)";
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(command);
    }

    public async Task<List<TargetModel>> GetTargetsByUidAsync(string? uid,
        CancellationToken cancellationToken = default)
    {
        string sql = $"select * from target_table where uid = {uid}";
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        var result = await conn.QueryAsync<TargetModel>(command);
        return result.ToList();
    }

    public async Task<TargetModel?> GetTargetsByUidAndTargetUidAsync(string? uid, string? targetUid,
        CancellationToken cancellationToken = default)
    {
        string sql = $"select * from target_table where uid = {uid} and target_uid = @TargetUid";
        var parameters = new { TargetUid = targetUid };
        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        var result = await conn.QuerySingleOrDefaultAsync<TargetModel?>(command);
        return result;
    }
}