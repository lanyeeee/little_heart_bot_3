using Dapper;
using little_heart_bot_3.Models;
using little_heart_bot_3.Others;
using MySqlConnector;

namespace little_heart_bot_3.Repositories.Implements;

public class UserRepository : IUserRepository
{
    private readonly ITargetRepository _targetRepository;
    private readonly IMessageRepository _messageRepository;

    public UserRepository(ITargetRepository targetRepository, IMessageRepository messageRepository)
    {
        _targetRepository = targetRepository;
        _messageRepository = messageRepository;
    }

    public async Task NewDayAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync("update user_table set completed = 0, config_num = 0 where 1");
    }

    public async Task<List<UserModel>> GetUncompletedUsersAsync(int num, CancellationToken cancellationToken = default)
    {
        string sql = $"select * from user_table where completed = 0 and cookie_status = 1 limit {num}";
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        var result = await conn.QueryAsync<UserModel>(sql);

        var users = result.ToList();

        foreach (var user in users)
        {
            user.Targets = await _targetRepository.GetTargetsByUidAsync(user.Uid, cancellationToken);
            user.Messages = await _messageRepository.GetMessagesByUidAsync(user.Uid, cancellationToken);
        }

        return users;
    }

    public async Task<List<UserModel>> GetUnverifiedUsersAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        var result = await conn.QueryAsync<UserModel>("select * from user_table where cookie_status = 0");

        var users = result.ToList();

        foreach (var user in users)
        {
            user.Targets = await _targetRepository.GetTargetsByUidAsync(user.Uid, cancellationToken);
            user.Messages = await _messageRepository.GetMessagesByUidAsync(user.Uid, cancellationToken);
        }

        return users;
    }

    public async Task MarkCookieErrorAsync(string? uid, CancellationToken cancellationToken = default)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync($"update user_table set cookie_status = -1 where uid = {uid}");
    }

    public async Task MarkCookieValidAsync(string? uid, CancellationToken cancellationToken = default)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync($"update user_table set cookie_status = 1 where uid = {uid}");
    }

    public async Task SetCompletedAsync(int completed, string? uid, CancellationToken cancellationToken = default)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync($"update user_table set completed = {completed} where uid = {uid}");
    }

    public async Task<UserModel?> GetAsync(string? uid, CancellationToken cancellationToken = default)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        string sql = $"select * from user_table where uid = {uid}";
        UserModel? user = await conn.QueryFirstOrDefaultAsync<UserModel?>(sql);

        if (user == null) return null;

        user.Targets = await _targetRepository.GetTargetsByUidAsync(uid, cancellationToken);
        user.Messages = await _messageRepository.GetMessagesByUidAsync(uid, cancellationToken);

        return user;
    }

    public async Task<List<UserModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        List<UserModel> users = new();
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        var uidResult = await conn.QueryAsync<string>("select uid from user_table where 1");
        foreach (string uid in uidResult)
        {
            UserModel? user = await GetAsync(uid, cancellationToken);
            if (user == null) continue;
            users.Add(user);
        }

        return users;
    }

    public async Task SetReadTimestampAsync(string readTimestamp, string uid, CancellationToken cancellationToken = default)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync($"update user_table set read_timestamp = {readTimestamp} where uid = {uid}");
    }

    public async Task InsertAsync(UserModel userModel, CancellationToken cancellationToken = default)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        string sql =
            "insert into user_table(uid, cookie, csrf, completed, cookie_status, config_num, read_timestamp, config_timestamp) values(@Uid, @Cookie, @Csrf, @Completed, @CookieStatus, @ConfigNum, @ReadTimestamp, @ConfigTimestamp)";
        await conn.ExecuteAsync(sql, userModel);
    }

    public async Task DeleteAsync(string? uid, CancellationToken cancellationToken = default)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync($"delete from user_table where uid = {uid}");
    }

    public async Task UpdateAsync(UserModel userModel, CancellationToken cancellationToken = default)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        string sql =
            "update user_table set cookie = @Cookie, csrf = @Csrf, completed = @Completed, cookie_status = @CookieStatus, config_num = @ConfigNum, read_timestamp = @ReadTimestamp, config_timestamp = @ConfigTimestamp where uid = @Uid";
        await conn.ExecuteAsync(sql, userModel);
    }
}