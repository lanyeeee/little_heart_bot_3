using little_heart_bot_3.ScheduleJobs;

namespace little_heart_bot_3.Others;

public static class Globals
{
    public static AppStatus? AppStatus { get; set; }
    public static BotStatus? BotStatus { get; set; }

    public const string DefaultMessageContent = "飘过~";

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cookie"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static string GetCsrf(string cookie)
    {
        return cookie.Substring(cookie.IndexOf("bili_jct=", StringComparison.Ordinal) + 9, 32);
    }
}