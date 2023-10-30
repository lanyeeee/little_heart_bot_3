using System.Text;
using System.Text.Json.Nodes;
using little_heart_bot_3.Models;
using little_heart_bot_3.Others;
using little_heart_bot_3.Repositories;
using Serilog.Core;

namespace little_heart_bot_3.Services.Implements;

public partial class UserService : IUserService
{
    private readonly Logger _logger;
    private readonly IUserRepository _userRepository;
    private readonly IMessageService _messageService;
    private readonly ITargetService _targetService;


    public UserService(Logger logger, IUserRepository userRepository, IMessageService messageService,
        ITargetService targetService)
    {
        _logger = logger;
        _userRepository = userRepository;
        _messageService = messageService;
        _targetService = targetService;
    }

    public async Task SendMessageAsync(UserModel user, CancellationToken cancellationToken = default)
    {
        if (user.Messages == null)
        {
            return;
        }

        foreach (var message in user.Messages)
        {
            try
            {
                //发弹幕之前先点个赞
                await _messageService.ThumbsUpAsync(message, user.Cookie, user.Csrf, cancellationToken);

                await Task.Delay(1000, cancellationToken);

                await _messageService.SendAsync(message, user.Cookie, user.Csrf, cancellationToken);
            }
            catch (LittleHeartException ex)
            {
                switch (ex.Reason)
                {
                    case Reason.CookieExpired:
                        _logger.Warning("uid {Uid} 的cookie已过期", message.Uid);
                        await MarkCookieError(message.Uid, cancellationToken);
                        //Cookie过期，不用再发了，直接返回，这个task正常结束
                        return;
                    case Reason.Ban:
                        //风控，抛出异常，由上层通过cancellationTokenSource.Cancel()来结束其他task
                        throw;
                }
            }
        }
    }

    public async Task WatchLiveAsync(UserModel user, CancellationToken cancellationToken = default)
    {
        UserModel? thisUser = await _userRepository.GetAsync(user.Uid, cancellationToken);

        if (thisUser?.CookieStatus != 1)
        {
            return;
        }

        if (user.Targets == null)
        {
            return;
        }

        //TODO: 改用Semaphore限制
        int maxCountPerRound = 10; //每个用户每轮最多同时观看多少个直播
        int selectedCount = 0; //已经在观看的直播数
        var tasks = new List<Task>();

        foreach (var target in user.Targets)
        {
            if (target.Completed == 1)
            {
                continue; //已完成的任务就跳过
            }

            try
            {
                tasks.Add(_targetService.StartAsync(target, user.Cookie, user.Csrf, cancellationToken));
                _logger.Verbose("uid {Uid} 开始观看 {TargetName} 的直播",
                    user.Uid, target.TargetName);

                await Task.Delay(500, cancellationToken);

                selectedCount++;
                if (selectedCount >= maxCountPerRound)
                {
                    break;
                }
            }
            catch (LittleHeartException ex)
            {
                switch (ex.Reason)
                {
                    case Reason.CookieExpired:
                        _logger.Warning("uid {Uid} 的cookie已过期", target.Uid);
                        await MarkCookieError(target.Uid, cancellationToken);
                        //Cookie过期，不用再看，直接返回，这个task正常结束
                        return;
                    case Reason.Ban:
                        //风控，抛出异常，由上层通过cancellationTokenSource.Cancel()来结束其他task
                        throw;
                }
            }
        }

        _logger.Information("uid {Uid} 正在观看直播，目前同时观看 {selectedCount} 个目标", user.Uid, selectedCount);

        //TODO: 这样即使有某个task出错了，也要等待所有task完成，这是不合理的
        await Task.WhenAll(tasks);

        //如果有任何一个任务未完成
        if (user.Targets.Any(t => t.Completed != 1))
        {
            return;
        }

        //如果所有任务都完成了
        user.Completed = 1;
        await _userRepository.SetCompletedAsync(user.Completed, user.Uid, cancellationToken);
    }

    public string? GetConfigString(UserModel user)
    {
        long nowTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        if (user.ConfigNum >= 10 || nowTimestamp - Int64.Parse(user.ConfigTimestamp!) < 60)
        {
            return null;
        }

        if (user.Targets == null)
        {
            return null;
        }

        string result = "";
        result += $"目标({user.Targets.Count}/50)：\n";
        user.Targets.ForEach(target => result += $"{target.TargetName}\n");

        if (result.Length > 350)
        {
            result =
                $"目标({user.Targets.Count}/50)：\n目标过多，信息超过了私信长度的上限，所以/config里无法携带目标的配置信息，请尝试使用/target_config查看目标配置\n";
        }

        result += "\n";
        if (string.IsNullOrEmpty(user.Cookie))
        {
            result += "cookie：无\n";
        }
        else
        {
            string cookieMsg = "";
            if (user.CookieStatus == -1) cookieMsg = "错误或已过期";
            else if (user.CookieStatus == 0) cookieMsg = "还未被使用";
            else if (user.CookieStatus == 1) cookieMsg = "直到上次使用还有效";
            result += $"cookie：有，{cookieMsg}\n";
        }


        string targetMsg = user.Completed == 1 ? "是" : "否";
        result += $"今日任务是否已完成：{targetMsg}\n";
        result += $"已用查询次数({user.ConfigNum + 1}/10)\n";
        return result;
    }

    public string? GetMessageConfigString(UserModel user)
    {
        long nowTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        if (user.ConfigNum >= 10 || nowTimestamp - Int64.Parse(user.ConfigTimestamp!) < 60)
        {
            return null;
        }

        if (user.Messages == null)
        {
            return null;
        }

        string result = "";
        result += $"弹幕({user.Messages.Count}/50)：\n\n";
        user.Messages.ForEach(message =>
        {
            result += $"{message.TargetName}：{message.Content}\n";
            string statusMsg;
            if (message.Response == null)
            {
                statusMsg = "未发送\n";
            }
            else
            {
                JsonNode response = JsonNode.Parse(message.Response)!;
                statusMsg = $"已发送，代码:{message.Code}，信息:{(string?)response["message"]}\n";
            }

            result += $"状态：{statusMsg}\n";
        });


        return result;
    }

    public List<string>? GetMessageConfigStringSplit(UserModel user)
    {
        string? content = GetMessageConfigString(user);

        if (content == null)
        {
            return null;
        }

        return SplitString(content, 400);
    }

    public async Task<string?> GetMessageConfigStringAsync(UserModel user, string targetUid,
        CancellationToken cancellationToken = default)
    {
        long nowTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        if (user.ConfigNum >= 10 || nowTimestamp - Int64.Parse(user.ConfigTimestamp!) < 60)
        {
            return null;
        }

        MessageModel? message =
            await _messageService.GetMessagesByUidAndTargetUid(user.Uid, targetUid, cancellationToken);
        if (message == null)
        {
            return null;
        }

        string result = $"{message.TargetName}：{message.Content}\n";
        string statusMsg;
        if (message.Response == null)
        {
            statusMsg = "未发送\n";
        }
        else
        {
            JsonNode response = JsonNode.Parse(message.Response)!;
            statusMsg = $"已发送，代码:{message.Code}，信息:{(string?)response["message"]}\n";
        }

        result += $"状态：{statusMsg}\n";
        return result;
    }

    public string? GetTargetConfigString(UserModel user)
    {
        long nowTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        if (user.ConfigNum >= 10 || nowTimestamp - Int64.Parse(user.ConfigTimestamp!) < 60)
        {
            return null;
        }

        if (user.Targets == null)
        {
            return null;
        }

        string result = "";
        result += $"目标({user.Targets.Count}/50)\n\n";
        result += "观看时长(分钟)：\n";
        user.Targets.ForEach(target => { result += $"{target.TargetName}：{target.WatchedSeconds / 60}\n"; });
        result += "\n";

        return result;
    }

    public List<string>? GetTargetConfigStringSplit(UserModel user)
    {
        string? content = GetTargetConfigString(user);

        if (content == null)
        {
            return null;
        }

        return SplitString(content, 400);
    }

    public async Task<string?> GetTargetConfigStringAsync(UserModel user, string targetUid,
        CancellationToken cancellationToken = default)
    {
        long nowTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        if (user.ConfigNum >= 10 || nowTimestamp - Int64.Parse(user.ConfigTimestamp!) < 60)
        {
            return null;
        }

        TargetModel? target =
            await _targetService.GetTargetsByUidAndTargetUidAsync(user.Uid, targetUid, cancellationToken);
        if (target == null)
        {
            return null;
        }

        string result = "观看时长(分钟)：\n";
        result += $"{target.TargetName}：{target.WatchedSeconds / 60}\n";
        result += "\n";

        return result;
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