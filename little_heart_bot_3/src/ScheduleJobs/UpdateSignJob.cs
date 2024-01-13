using little_heart_bot_3.Data;
using little_heart_bot_3.Data.Models;
using little_heart_bot_3.Others;
using little_heart_bot_3.Services;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace little_heart_bot_3.ScheduleJobs;

public class UpdateSignJob : IJob
{
    private readonly IBotService _botService;
    private readonly BotModel _botModel;

    public UpdateSignJob(IBotService botService, IDbContextFactory<LittleHeartDbContext> dbContextFactory)
    {
        _botService = botService;
        using var db = dbContextFactory.CreateDbContext();
        BotModel? botModel = db.Bots.SingleOrDefault();

        _botModel = botModel ?? throw new LittleHeartException("数据库bot_table表中没有数据，请自行添加");
    }

    public async Task Execute(IJobExecutionContext context)
    {
        Console.WriteLine("UpdateSignJob");
        await _botService.UpdateSignAsync(_botModel);
    }
}