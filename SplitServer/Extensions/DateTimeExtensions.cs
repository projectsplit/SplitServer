namespace SplitServer.Extensions;

public static class DateTimeExtensions
{
    public static DateTime ToUtc(this DateTime localDateTime, string timeZoneId)
    {
        var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        // Completely recreate the DateTime to remove any timezone context
        var cleanDateTime = new DateTime(
            localDateTime.Year,
            localDateTime.Month,
            localDateTime.Day,
            localDateTime.Hour,
            localDateTime.Minute,
            localDateTime.Second,
            localDateTime.Millisecond,
            DateTimeKind.Unspecified);

        return TimeZoneInfo.ConvertTimeToUtc(cleanDateTime, timeZoneInfo);
    }

    public static DateTime EndOfDay(this DateTime date)
    {
        return date.Date.AddDays(1).AddTicks(-1);
    }
}