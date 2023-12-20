﻿using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using little_heart_bot_3.Data;
using little_heart_bot_3.Data.Models;
using little_heart_bot_3.Others;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace little_heart_bot_3.Services.Implements;

public class UserService : IUserService
{
    private readonly IServiceProvider _provider;
    private readonly ILogger _logger;
    private readonly IMessageService _messageService;
    private readonly ITargetService _targetService;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _options;


    public UserService(
        IServiceProvider provider,
        ILogger logger,
        IMessageService messageService,
        ITargetService targetService,
        JsonSerializerOptions options,
        HttpClient httpClient)
    {
        _provider = provider;
        _logger = logger;
        _messageService = messageService;
        _targetService = targetService;
        _options = options;
        _httpClient = httpClient;
    }

    public async Task SendMessageAsync(UserModel user, CancellationToken cancellationToken = default)
    {
        var db = new LittleHeartDbContext();
        db.Attach(user);

        foreach (var message in user.Messages)
        {
            try
            {
                //发弹幕之前先点个赞
                await _messageService.ThumbsUpAsync(message, cancellationToken);
                await Task.Delay(100, cancellationToken);

                await _messageService.SendAsync(message, cancellationToken);
                await Task.Delay(900, cancellationToken);
            }
            catch (LittleHeartException ex)
            {
                switch (ex.Reason)
                {
                    case Reason.CookieExpired:
                        _logger.Warning("uid {Uid} 的cookie已过期", message.Uid);
                        user.CookieStatus = CookieStatus.Error;
                        db.Users.Update(user);
                        await db.SaveChangesAsync(cancellationToken);
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
        var db = new LittleHeartDbContext();
        db.Users.Attach(user);

        //TODO: 改用Semaphore限制
        int maxCountPerRound = 10; //每个用户每轮最多同时观看多少个直播
        int selectedCount = 0; //已经在观看的直播数
        var tasks = new List<Task>();

        foreach (var target in user.Targets)
        {
            if (target.Completed)
            {
                continue; //已完成的任务就跳过
            }

            var task = _targetService.StartAsync(target, cancellationToken);
            tasks.Add(task);
            _logger.Verbose("uid {Uid} 开始观看 {TargetName} 的直播",
                user.Uid, target.TargetName);

            await Task.Delay(500, cancellationToken);

            selectedCount++;
            if (selectedCount >= maxCountPerRound)
            {
                break;
            }
        }

        _logger.Information("uid {Uid} 正在观看直播，目前同时观看 {SelectedCount} 个目标", user.Uid, selectedCount);

        try
        {
            while (tasks.Count != 0)
            {
                var finishedTask = await Task.WhenAny(tasks);
                tasks.Remove(finishedTask);

                await finishedTask;
            }

            //如果有任何一个任务未完成
            if (user.Targets.Any(t => !t.Completed))
            {
                return;
            }

            //如果所有任务都完成了
            _logger.Information("uid {Uid} 今日的所有任务已完成", user.Uid);
            user.Completed = true;
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (LittleHeartException ex)
        {
            switch (ex.Reason)
            {
                case Reason.CookieExpired:
                    _logger.Warning("uid {Uid} 的cookie已过期", user.Uid);
                    user.CookieStatus = CookieStatus.Error;
                    await db.SaveChangesAsync(CancellationToken.None);
                    //Cookie过期，不用再看，直接返回，这个task正常结束
                    return;
                case Reason.Ban:
                    //风控，抛出异常，由上层通过cancellationTokenSource.Cancel()来结束其他task
                    throw;
            }
        }
    }

    public async Task<JsonNode?> GetOtherUserInfoAsync(UserModel user, long uid,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string> { { "mid", uid.ToString() } };
        string queryString = await Wbi.GetWbiQueryStringAsync(_httpClient, parameters, cancellationToken);

        HttpResponseMessage responseMessage = await _httpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri($"https://api.bilibili.com/x/space/wbi/acc/info?{queryString}"),
            Headers =
            {
                {
                    "user-agent",
                    "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/102.0.0.0 Safari/537.36"
                },
                { "cookie", user.Cookie }
            },
        }, cancellationToken);
        await Task.Delay(1000, cancellationToken);

        JsonNode? response = JsonNode.Parse(await responseMessage.Content.ReadAsStringAsync(cancellationToken));
        if (response == null)
        {
            throw new LittleHeartException(Reason.NullResponse);
        }

        int code = (int)response["code"]!;

        if (code is -400 or -404)
        {
            _logger.Error(new Exception(response.ToJsonString(_options)),
                "uid {uid} 获取 {targetUid} 的直播间数据失败",
                user.Uid,
                uid);
            return null;
        }

        if (code != 0)
        {
            _logger.Error(new Exception(response.ToJsonString(_options)),
                "uid {uid} 获取 {targetUid} 的直播间数据失败",
                user.Uid,
                uid);
            throw new LittleHeartException(Reason.Ban);
        }

        return response["data"];
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
            if (message.Response == null)
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
        if (message.Response == null)
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

        if (content == null)
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

        if (content == null)
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