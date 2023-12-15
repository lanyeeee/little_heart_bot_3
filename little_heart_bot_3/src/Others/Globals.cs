using little_heart_bot_3.Data.Models;

namespace little_heart_bot_3.Others;

public static class Globals
{
    public static AppStatus AppStatus { get; set; }
    public static ReceiveStatus ReceiveStatus { get; set; }
    public static SendStatus SendStatus { get; set; }

    public const string DefaultMessageContent = "飘过~";

    public const int TotalSecondInOneDay = 24 * 60 * 60;

    public static string GetCsrf(string cookie)
    {
        return cookie.Substring(cookie.IndexOf("bili_jct=", StringComparison.Ordinal) + 9, 32);
    }

    static Globals()
    {
    }
}