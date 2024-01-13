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

    public UpdateSignJob(IBotService botService, IConfiguration configuration)
    {
        _botService = botService;
        _botModel = BotModel.LoadFromConfiguration(configuration);
    }

    public async Task Execute(IJobExecutionContext context)
    {
        await _botService.UpdateSignAsync(_botModel);
    }
}