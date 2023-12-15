using little_heart_bot_3.Data.Models;

namespace little_heart_bot_3.Others;

public static class Globals
{
    public static AppStatus AppStatus { get; set; }
    public static ReceiveStatus ReceiveStatus { get; set; }
    public static SendStatus SendStatus { get; set; }

    public const string DefaultMessageContent = "飘过~";


    static Globals()
    {
    }
}