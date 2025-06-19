namespace SplitServer.Extensions;

public static class DateTimeExtensions
{
    public static DateTime ToUtc(this DateTime localDateTime, string timeZoneId)
    {
        var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        return TimeZoneInfo.ConvertTimeToUtc(localDateTime, timeZoneInfo);
    }
}