namespace little_heart_bot_3.others;

public static class ExtensionMethods
{
    public static bool IsNumeric(this string value)
    {
        return value.All(char.IsNumber);
    }
}