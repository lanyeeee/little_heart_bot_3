using Dapper;
using little_heart_bot_3.Models;
using little_heart_bot_3.Others;
using MySqlConnector;

namespace little_heart_bot_3.Repositories.Implements;

public class BotRepository : IBotRepository
{
    public async Task<BotModel?> GetBotAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = new MySqlConnection(Globals.ConnectionString);
        return await conn.QueryFirstOrDefaultAsync(new CommandDefinition("select * from bot_table",
            cancellationToken: cancellationToken));
    }
}