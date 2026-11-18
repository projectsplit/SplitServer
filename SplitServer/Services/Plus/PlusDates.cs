namespace SplitServer.Services.Plus;

/// <summary>
/// Turns a moment into the date a particular person would call it. Every date a Plus notice names
/// goes through here, because "the 12th" is a different day depending on where the reader is, and
/// the day they are told should be the one they will see it happen on.
/// </summary>
public static class PlusDates
{
    /// <summary>
    /// <paramref name="utc"/> on the clock of <paramref name="timeZoneId"/>. A zone that is missing
    /// or that this machine does not know falls back to UTC rather than failing whatever notice was
    /// being written: a date that might be a day out is better than no notice at all.
    /// </summary>
    public static DateTime ToLocal(DateTime utc, string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return utc;
        }

        try
        {
            return TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utc, DateTimeKind.Utc),
                TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return utc;
        }
    }
}
