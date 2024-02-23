using Polly;
using Serilog.Events;

namespace little_heart_bot_3.Others;

public static class ExtensionMethods
{
    public static bool IsNumeric(this string value)
    {
        return value.All(char.IsNumber);
    }

    public static void LogWithContext(this ILogger logger, Action logAction,
        params KeyValuePair<string, object>[] contextDataParams)
    {
        var contextData = new Dictionary<string, object>();
        foreach (var kvp in contextDataParams)
        {
            contextData.TryAdd(kvp.Key, kvp.Value);
        }

        using (logger.BeginScope(contextData))
        {
            logAction.Invoke();
        }
    }

    public static void LogWithResponse(this ILogger logger, Action logAction, string response)
    {
        using (logger.BeginScope(new Dictionary<string, object> { ["Response"] = response }))
        {
            logAction.Invoke();
        }
    }

    public static HttpRequestMessage SetRetryCallback(this HttpRequestMessage request,
        Action<DelegateResult<HttpResponseMessage>, TimeSpan, int> callback)
    {
        request.SetPolicyExecutionContext(new Context
        {
            ["callback"] = callback
        });
        return request;
    }

    public static bool SourceContextEquals(this LogEvent logEvent, Type sourceContext)
    {
        return logEvent.Properties.GetValueOrDefault("SourceContext") is ScalarValue sv &&
               sv.Value?.ToString() == sourceContext.FullName;
    }
}