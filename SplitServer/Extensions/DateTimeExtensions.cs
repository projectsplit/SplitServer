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

        // A local time DST skipped over does not exist, and ConvertTimeToUtc throws on it. Zones
        // that spring forward at midnight (Havana, Santiago, Cairo) would otherwise take down every
        // window that starts on that day, and a budget keeps the same start date for its whole cycle.
        if (timeZoneInfo.IsInvalidTime(cleanDateTime))
        {
            cleanDateTime = cleanDateTime.AddHours(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(cleanDateTime, timeZoneInfo);
    }

    public static DateTime EndOfDay(this DateTime date)
    {
        return date.Date.AddDays(1).AddTicks(-1);
    }
}