using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using little_heart_bot_3.Data;
using little_heart_bot_3.Data.Models;
using little_heart_bot_3.Others;
using Microsoft.EntityFrameworkCore;

namespace little_heart_bot_3.Services.Implements;

public abstract class UserService : IUserService
{
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _options;
    private readonly IMessageService _messageService;
    private readonly ITargetService _targetService;
    private readonly IApiService _apiService;
    private readonly IDbContextFactory<LittleHeartDbContext> _dbContextFactory;

    protected UserService(ILogger logger,
        JsonSerializerOptions options,
        IMessageService messageService,
        ITargetService targetService,
        IApiService apiService,
        IDbContextFactory<LittleHeartDbContext> dbContextFactory)
    {
        _logger = logger;
        _options = options;
        _messageService = messageService;
        _targetService = targetService;
        _apiService = apiService;
        _dbContextFactory = dbContextFactory;
    }

    public async Task SendMessageAsync(UserModel user, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.Users.Attach(user);

        foreach (var message in user.Messages)
        {
            try
            {
                //发弹幕之前先点个赞
                // await _messageService.ThumbsUpAsync(message, cancellationToken);
                // await Task.Delay(100, cancellationToken);

                await _messageService.SendAsync(message, cancellationToken);
                await Task.Delay(900, cancellationToken);
            }
            catch (LittleHeartException ex) when (ex.Reason == Reason.UserCookieExpired)
            {
                _logger.LogInformation("uid {Uid} 的cookie已过期", message.Uid);
                user.CookieStatus = CookieStatus.Error;
                //Cookie过期，不用再发了，直接返回，这个task正常结束
                return;
            }
            finally
            {
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }


    public async Task WatchLiveAsync(UserModel user, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.Users.Attach(user);

        try
        {
            var target = user.Targets.FirstOrDefault(t => !t.Completed);
            if (target is not null)
            {
                await _targetService.StartAsync(target, cancellationToken);
            }

            //如果有任何一个目标未完成
            if (user.Targets.Exists(t => !t.Completed))
            {
                return;
            }

            //如果所有目标都完成了
            _logger.LogInformation("uid {Uid} 今日的所有任务已完成", user.Uid);
            user.Completed = true;
        }
        catch (LittleHeartException ex) when (ex.Reason == Reason.UserCookieExpired)
        {
            //Cookie过期，不用再看，直接返回，这个task正常结束
            _logger.LogInformation("uid {Uid} 的cookie已过期", user.Uid);
            user.CookieStatus = CookieStatus.Error;
        }
        finally
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public string? GetConfigAllString(UserModel user)
    {
        long nowTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        if (user.ConfigNum >= 10 || nowTimestamp - user.ConfigTimestamp < 60)
        {
            return null;
        }

        var stringBuilder = new StringBuilder();

        stringBuilder.Append($"目标({user.Targets.Count}/50)：\n");
        user.Targets.ForEach(target => stringBuilder.Append($"{target.TargetName}\n"));

        if (stringBuilder.Length > 350)
        {
            stringBuilder.Clear();
            stringBuilder.Append(
                $"目标({user.Targets.Count}/50)：\n目标过多，信息超过了私信长度的上限，所以/config里无法携带目标的配置信息，请尝试使用/target_config查看目标配置\n");
        }

        stringBuilder.Append('\n');
        if (string.IsNullOrEmpty(user.Cookie))
        {
            stringBuilder.Append("cookie：无\n");
        }
        else
        {
            string cookieMsg = user.CookieStatus switch
            {
                CookieStatus.Error => "错误或已过期",
                CookieStatus.Unverified => "还未被使用",
                CookieStatus.Normal => "直到上次使用还有效",
                _ => ""
            };
            stringBuilder.Append($"cookie：有，{cookieMsg}\n");
        }

        string targetMsg = user.Completed ? "是" : "否";
        stringBuilder.Append($"今日任务是否已完成：{targetMsg}\n");
        stringBuilder.Append($"已用查询次数({user.ConfigNum + 1}/10)\n");
        return stringBuilder.ToString();
    }

    public string? GetAllMessageConfigString(UserModel user)
    {
        long nowTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        if (user.ConfigNum >= 10 || nowTimestamp - user.ConfigTimestamp < 60)
        {
            return null;
        }

        StringBuilder stringBuilder = new();
        stringBuilder.Append($"弹幕({user.Messages.Count}/50)：\n\n");
        user.Messages.ForEach(message =>
        {
            stringBuilder.Append($"{message.TargetName}：{message.Content}\n");
            string statusMsg;
            if (message.Response is null)
            {
                statusMsg = "未发送\n";
            }
            else
            {
                JsonNode response = JsonNode.Parse(message.Response)!;
                statusMsg = $"已发送，代码:{message.Code}，信息:{(string?)response["message"]}\n";
            }

            stringBuilder.Append($"状态：{statusMsg}\n");
        });


        return stringBuilder.ToString();
    }

    public string? GetSpecifyMessageConfigString(MessageModel message)
    {
        UserModel user = message.UserModel;
        long nowTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        if (user.ConfigNum >= 10 || nowTimestamp - user.ConfigTimestamp < 60)
        {
            return null;
        }

        StringBuilder stringBuilder = new();
        stringBuilder.Append($"{message.TargetName}：{message.Content}\n");
        string statusMsg;
        if (message.Response is null)
        {
            statusMsg = "未发送\n";
        }
        else
        {
            JsonNode response = JsonNode.Parse(message.Response)!;
            statusMsg = $"已发送，代码:{message.Code}，信息:{(string?)response["message"]}\n";
        }

        stringBuilder.Append($"状态：{statusMsg}\n");
        return stringBuilder.ToString();
    }

    public List<string>? GetAllMessageConfigStringSplit(UserModel user)
    {
        string? content = GetAllMessageConfigString(user);

        if (content is null)
        {
            return null;
        }

        return SplitString(content, 400);
    }

    public string? GetAllTargetConfigString(UserModel user)
    {
        long nowTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        if (user.ConfigNum >= 10 || nowTimestamp - user.ConfigTimestamp < 60)
        {
            return null;
        }

        StringBuilder stringBuilder = new();

        stringBuilder.Append($"目标({user.Targets.Count}/50)\n\n");
        stringBuilder.Append("观看时长(分钟)：\n");
        user.Targets.ForEach(target => stringBuilder.Append($"{target.TargetName}：{target.WatchedSeconds / 60}\n"));
        stringBuilder.Append('\n');

        return stringBuilder.ToString();
    }

    public string? GetSpecifyTargetConfigString(TargetModel target)
    {
        UserModel user = target.UserModel;
        long nowTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        if (user.ConfigNum >= 10 || nowTimestamp - user.ConfigTimestamp < 60)
        {
            return null;
        }

        StringBuilder stringBuilder = new();
        stringBuilder.Append("观看时长(分钟)：\n");
        stringBuilder.Append($"{target.TargetName}：{target.WatchedSeconds / 60}\n");
        stringBuilder.Append('\n');

        return stringBuilder.ToString();
    }

    public List<string>? GetAllTargetConfigStringSplit(UserModel user)
    {
        string? content = GetAllTargetConfigString(user);

        if (content is null)
        {
            return null;
        }

        return SplitString(content, 400);
    }

    private List<string> SplitString(string config, int maxLength)
    {
        // 将字符串按照换行符分割成行
        string[] lines = config.Split("\n");

        // 将行重新拼接成长度不超过maxLength的小段
        List<string> contents = new List<string>();
        StringBuilder contentBuilder = new StringBuilder();
        foreach (string line in lines)
        {
            if (contentBuilder.Length + line.Length > maxLength)
            {
                contents.Add(contentBuilder.ToString());
                contentBuilder.Clear();
            }

            contentBuilder.Append(line);
            contentBuilder.Append('\n');
        }

        contents.Add(contentBuilder.ToString());
        return contents;
    }
}