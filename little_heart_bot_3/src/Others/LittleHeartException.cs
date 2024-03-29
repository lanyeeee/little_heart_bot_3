namespace little_heart_bot_3.Others;

public class LittleHeartException : Exception
{
    public Reason Reason { get; set; }

    public LittleHeartException(Reason reason)
        : base(reason switch
        {
            Reason.RiskControl => "RiskControl",
            Reason.UserCookieExpired => "UserCookieExpired",
            Reason.BotCookieExpired => "BotCookieExpired",
            Reason.WithoutMedal => "WithoutMedal",
            _ => "Null"
        })
    {
        Reason = reason;
    }


    public LittleHeartException(string? message, Reason reason) : base(message)
    {
        Reason = reason;
    }

    public LittleHeartException(string? message, Exception? innerException, Reason reason)
        : base(message, innerException)
    {
        Reason = reason;
    }
}

public enum Reason
{
    RiskControl,
    UserCookieExpired,
    WithoutMedal,
    BotCookieExpired,
}