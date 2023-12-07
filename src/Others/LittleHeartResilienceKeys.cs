using little_heart_bot_3.Data.Models;
using Polly;

namespace little_heart_bot_3.Others;

public static class LittleHeartResilienceKeys
{
    public static readonly ResiliencePropertyKey<TargetModel?> Target = new("target");
    public static readonly ResiliencePropertyKey<UserModel?> User = new("user");
    public static readonly ResiliencePropertyKey<MessageModel?> Message = new("message");
    public static readonly ResiliencePropertyKey<BotModel?> Bot = new("bot");
}