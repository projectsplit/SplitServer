namespace SplitServer.Models;

/// <summary>
/// When a recurring expense fires, as the user described it rather than as an offset from whenever
/// they happened to set it up. Interpreted in the template's time zone, so "every month on the 1st
/// at 09:00" stays 09:00 across DST and stays the 1st regardless of month length.
/// </summary>
public record RecurrenceSchedule
{
    public required RecurrenceFrequency Frequency { get; init; }

    public required int Hour { get; init; }
    public required int Minute { get; init; }

    /// <summary>Weekly and biweekly only.</summary>
    public DayOfWeek? DayOfWeek { get; init; }

    /// <summary>Monthly and annually only. Clamped down in months that are too short.</summary>
    public int? DayOfMonth { get; init; }

    /// <summary>Annually only. 1-12.</summary>
    public int? Month { get; init; }
}
