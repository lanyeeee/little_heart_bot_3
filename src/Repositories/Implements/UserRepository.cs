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
        var command = new CommandDefinition("update user_table set completed = 0, config_num = 0 where 1",
            cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(command);
    }

    public async Task<List<UserModel>> GetUncompletedUsersAsync(int num, CancellationToken cancellationToken = default)
    {
        string sql = $"select * from user_table where completed = 0 and cookie_status = 1 limit {num}";
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
        await using var conn = new MySqlConnection(Globals.ConnectionString);

        var result = await conn.QueryAsync<UserModel>(command);
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
        string sql = "select * from user_table where cookie_status = 0";
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        var result = await conn.QueryAsync<UserModel>(command);
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
        string sql = $"update user_table set cookie_status = -1 where uid = {uid}";
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(command);
    }

    public async Task MarkCookieValidAsync(string? uid, CancellationToken cancellationToken = default)
    {
        string sql = $"update user_table set cookie_status = 1 where uid = {uid}";
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(command);
    }

    public async Task SetCompletedAsync(int completed, string? uid, CancellationToken cancellationToken = default)
    {
        string sql = $"update user_table set completed = {completed} where uid = {uid}";
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(command);
    }

    public async Task<UserModel?> GetAsync(string? uid, CancellationToken cancellationToken = default)
    {
        string sql = $"select * from user_table where uid = {uid}";
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        UserModel? user = await conn.QueryFirstOrDefaultAsync<UserModel?>(command);
        if (user == null)
        {
            return null;
        }

        user.Targets = await _targetRepository.GetTargetsByUidAsync(uid, cancellationToken);
        user.Messages = await _messageRepository.GetMessagesByUidAsync(uid, cancellationToken);

        return user;
    }

    public async Task<List<UserModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        string sql = "select uid from user_table where 1";
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
        List<UserModel> users = new();

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        var uidResult = await conn.QueryAsync<string>(command);
        foreach (string uid in uidResult)
        {
            UserModel? user = await GetAsync(uid, cancellationToken);
            if (user == null)
            {
                continue;
            }

            users.Add(user);
        }

        return users;
    }

    public async Task SetReadTimestampAsync(string readTimestamp, string uid,
        CancellationToken cancellationToken = default)
    {
        string sql = $"update user_table set read_timestamp = {readTimestamp} where uid = {uid}";
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(command);
    }

    public async Task InsertAsync(UserModel userModel, CancellationToken cancellationToken = default)
    {
        string sql =
            "insert into user_table(uid, cookie, csrf, completed, cookie_status, config_num, read_timestamp, config_timestamp) values(@Uid, @Cookie, @Csrf, @Completed, @CookieStatus, @ConfigNum, @ReadTimestamp, @ConfigTimestamp)";
        var command = new CommandDefinition(sql, userModel, cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(command);
    }

    public async Task DeleteAsync(string? uid, CancellationToken cancellationToken = default)
    {
        string sql = $"delete from user_table where uid = {uid}";
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(command);
    }

    public async Task UpdateAsync(UserModel userModel, CancellationToken cancellationToken = default)
    {
        string sql =
            "update user_table set cookie = @Cookie, csrf = @Csrf, completed = @Completed, cookie_status = @CookieStatus, config_num = @ConfigNum, read_timestamp = @ReadTimestamp, config_timestamp = @ConfigTimestamp where uid = @Uid";
        var command = new CommandDefinition(sql, userModel, cancellationToken: cancellationToken);

        await using var conn = new MySqlConnection(Globals.ConnectionString);
        await conn.ExecuteAsync(command);
    }
}