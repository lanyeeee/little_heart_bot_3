using little_heart_bot_3.others;

namespace little_heart_bot_3.entity;

public class UserEntity
{
    public string? Uid { get; set; }
    public string? Cookie { get; set; }
    public string? Csrf { get; set; }
    public int Completed { get; set; }
    public int CookieStatus { get; set; }
    public int ConfigNum { get; set; }
    public int TargetNum { get; set; }
    public string? ReadTimestamp { get; set; }
    public string? ConfigTimestamp { get; set; }

    public async Task SendMessage(Logger logger)
    {
        List<MessageEntity> messages = await Globals.MessageRepository.GetMessagesByUid(Uid);
        foreach (var message in messages)
        {
            await message.Send(Cookie, Csrf, logger);
        }
    }

    public async Task WatchLive(Logger logger)
    {
        UserEntity thisUser = await Globals.UserRepository.Get(Uid);
        if (thisUser.CookieStatus != 1) return;

        List<TargetEntity> targets = await Globals.TargetRepository.GetUncompletedTargetsByUid(Uid);
#if DEBUG
        Console.WriteLine($"uid {Uid}: targets.Count={targets.Count}");
#endif
        var tasks = new List<Task>();
        targets.ForEach(target => tasks.Add(target.Start(Cookie, Csrf, logger)));
        await Task.WhenAll(tasks);

        targets = await Globals.TargetRepository.GetUncompletedTargetsByUid(Uid);
        if (targets.Count != 0) return;

        Completed = 1;
        await Globals.UserRepository.SetCompleted(Completed, Uid);
    }
}