using Coravel.Invocable;
using little_heart_bot_3.Data;
using Microsoft.EntityFrameworkCore;

namespace little_heart_bot_3.ScheduleJobs;

public class NewDayJob : IInvocable
{
    private readonly IDbContextFactory<LittleHeartDbContext> _dbContextFactory;

    public NewDayJob(IDbContextFactory<LittleHeartDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task Invoke()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);

        await foreach (var message in db.Messages)
        {
            message.Code = 0;
            message.Response = null;
            message.Completed = false;
        }

        await foreach (var target in db.Targets)
        {
            target.Exp = 0;
            target.WatchedSeconds = 0;
            target.Completed = false;
        }

        await foreach (var user in db.Users)
        {
            user.Completed = false;
            user.ConfigNum = 0;
        }

        await db.SaveChangesAsync(CancellationToken.None);
    }
}