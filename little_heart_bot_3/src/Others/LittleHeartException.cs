namespace little_heart_bot_3.Others;

public class LittleHeartException : Exception
{
    public Reason Reason { get; set; }

    public LittleHeartException() : base(null)
    {
    }

    public LittleHeartException(Reason reason)
        : base(reason switch
        {
            Reason.Ban => "Ban",
            Reason.NullResponse => "NullResponse",
            Reason.CookieExpired => "CookieExpired",
            Reason.WithoutMedal => "WithoutMedal",
            Reason.ServerTimeout => "ServerTimeout",
            _ => "Null"
        })
    {
        Reason = reason;
    }

    public LittleHeartException(string? message) : base(message)
    {
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
    Ban,
    NullResponse,
    CookieExpired,
    WithoutMedal,
    ServerTimeout
}