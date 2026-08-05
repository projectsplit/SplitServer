using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Services;

/// <summary>
/// Resolves a <see cref="RecurrenceSchedule"/> into actual instants.
/// </summary>
/// <remarks>
/// Everything is computed in the template's own time zone and only converted to UTC at the end. A
/// schedule is a wall clock statement — "the 1st at 09:00" — so stepping in UTC would drift the
/// hour across DST, and stepping from the previous occurrence would drift the day: 31 January plus
/// a month clamps to 28 February, and clamping again from there would land on 28 March. Each step
/// is therefore recomputed from the schedule rather than from the instance before it.
/// </remarks>
public static class RecurrenceCalculator
{
    /// <summary>A leap year, so 29 February is accepted and clamped later in the years that lack it.</summary>
    private const int LeapYearForValidation = 2024;

    public static Result Validate(RecurrenceSchedule schedule)
    {
        if (schedule.Hour is < 0 or > 23)
        {
            return Result.Failure("Hour must be between 0 and 23");
        }

        if (schedule.Minute is < 0 or > 59)
        {
            return Result.Failure("Minute must be between 0 and 59");
        }

        switch (schedule.Frequency)
        {
            case RecurrenceFrequency.Daily:
                return Result.Success();

            case RecurrenceFrequency.Weekly:
            case RecurrenceFrequency.Biweekly:
                return schedule.DayOfWeek is null
                    ? Result.Failure("A day of the week is required for weekly and biweekly expenses")
                    : Result.Success();

            case RecurrenceFrequency.Monthly:
                if (schedule.DayOfMonth is null)
                {
                    return Result.Failure("A day of the month is required for monthly expenses");
                }

                return schedule.DayOfMonth is < 1 or > 31
                    ? Result.Failure("Day of the month must be between 1 and 31")
                    : Result.Success();

            case RecurrenceFrequency.Annually:
                if (schedule.Month is null || schedule.DayOfMonth is null)
                {
                    return Result.Failure("A month and a day are required for annual expenses");
                }

                if (schedule.Month is < 1 or > 12)
                {
                    return Result.Failure("Month must be between 1 and 12");
                }

                var maxDay = DateTime.DaysInMonth(LeapYearForValidation, schedule.Month.Value);

                return schedule.DayOfMonth < 1 || schedule.DayOfMonth > maxDay
                    ? Result.Failure($"Day must be between 1 and {maxDay} for the selected month")
                    : Result.Success();

            default:
                return Result.Failure("Unexpected recurrence frequency");
        }
    }

    /// <summary>
    /// The earliest slot matching the schedule that falls strictly after <paramref name="nowUtc"/>.
    /// Strictly, so setting something up at exactly its own slot does not fire in the same instant.
    /// </summary>
    public static DateTime GetFirstOccurrence(DateTime nowUtc, RecurrenceSchedule schedule, string timeZoneId)
    {
        var timeZoneInfo = ResolveTimeZone(timeZoneId);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZoneInfo);

        var candidate = BuildCandidate(localNow, schedule);

        if (candidate <= localNow)
        {
            candidate = StepOnePeriod(candidate, schedule);
        }

        return ToUtc(candidate, timeZoneInfo);
    }

    public static DateTime GetNext(DateTime currentUtc, RecurrenceSchedule schedule, string timeZoneId)
    {
        var timeZoneInfo = ResolveTimeZone(timeZoneId);
        var localCurrent = TimeZoneInfo.ConvertTimeFromUtc(currentUtc, timeZoneInfo);

        return ToUtc(StepOnePeriod(localCurrent, schedule), timeZoneInfo);
    }

    /// <summary>
    /// Walks the schedule forward until it lands after <paramref name="nowUtc"/>, and reports how
    /// many occurrences were skipped. A template that went unprocessed while the server was down
    /// resumes on its original cadence instead of drifting to whenever it was noticed, and the
    /// backlog is not replayed as a burst of duplicate expenses.
    /// </summary>
    public static (DateTime NextOccurrence, int SkippedOccurrences) CatchUp(
        DateTime nextOccurrenceUtc,
        DateTime nowUtc,
        RecurrenceSchedule schedule,
        string timeZoneId)
    {
        var next = GetNext(nextOccurrenceUtc, schedule, timeZoneId);
        var skipped = 0;

        // Bounded so a corrupt schedule far in the past can never spin the worker.
        const int maxSteps = 5000;

        while (next <= nowUtc && skipped < maxSteps)
        {
            next = GetNext(next, schedule, timeZoneId);
            skipped++;
        }

        return (next, skipped);
    }

    /// <summary>The slot in the period <paramref name="localFrom"/> already sits in.</summary>
    private static DateTime BuildCandidate(DateTime localFrom, RecurrenceSchedule schedule)
    {
        var timeOfDay = new TimeSpan(schedule.Hour, schedule.Minute, 0);

        switch (schedule.Frequency)
        {
            case RecurrenceFrequency.Daily:
                return localFrom.Date + timeOfDay;

            case RecurrenceFrequency.Weekly:
            case RecurrenceFrequency.Biweekly:
            {
                var daysUntil = ((int)schedule.DayOfWeek!.Value - (int)localFrom.DayOfWeek + 7) % 7;
                return localFrom.Date.AddDays(daysUntil) + timeOfDay;
            }

            case RecurrenceFrequency.Monthly:
                return OnDayOfMonth(localFrom.Year, localFrom.Month, schedule.DayOfMonth!.Value) + timeOfDay;

            case RecurrenceFrequency.Annually:
                return OnDayOfMonth(localFrom.Year, schedule.Month!.Value, schedule.DayOfMonth!.Value) + timeOfDay;

            default:
                throw new ArgumentOutOfRangeException(nameof(schedule), schedule.Frequency, "Unexpected recurrence frequency");
        }
    }

    private static DateTime StepOnePeriod(DateTime localCurrent, RecurrenceSchedule schedule)
    {
        var timeOfDay = new TimeSpan(schedule.Hour, schedule.Minute, 0);

        switch (schedule.Frequency)
        {
            case RecurrenceFrequency.Daily:
                return localCurrent.Date.AddDays(1) + timeOfDay;

            case RecurrenceFrequency.Weekly:
                return localCurrent.Date.AddDays(7) + timeOfDay;

            case RecurrenceFrequency.Biweekly:
                return localCurrent.Date.AddDays(14) + timeOfDay;

            case RecurrenceFrequency.Monthly:
            {
                // Rebuilt from the first of the next month rather than added to the current day, so
                // a clamped short month never drags the following ones down with it.
                var firstOfNextMonth = new DateTime(localCurrent.Year, localCurrent.Month, 1).AddMonths(1);
                return OnDayOfMonth(firstOfNextMonth.Year, firstOfNextMonth.Month, schedule.DayOfMonth!.Value) + timeOfDay;
            }

            case RecurrenceFrequency.Annually:
                return OnDayOfMonth(localCurrent.Year + 1, schedule.Month!.Value, schedule.DayOfMonth!.Value) + timeOfDay;

            default:
                throw new ArgumentOutOfRangeException(nameof(schedule), schedule.Frequency, "Unexpected recurrence frequency");
        }
    }

    private static DateTime OnDayOfMonth(int year, int month, int dayOfMonth)
    {
        return new DateTime(year, month, Math.Min(dayOfMonth, DateTime.DaysInMonth(year, month)));
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception)
        {
            // An unrecognised zone must not strand the schedule. UTC keeps the cadence correct and
            // only costs the user an offset in the time of day.
            return TimeZoneInfo.Utc;
        }
    }

    private static DateTime ToUtc(DateTime local, TimeZoneInfo timeZoneInfo)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

        // A local time DST skipped over does not exist; nudging past the gap is closer to the
        // user's intent than dropping the occurrence.
        if (timeZoneInfo.IsInvalidTime(unspecified))
        {
            unspecified = unspecified.AddHours(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(unspecified, timeZoneInfo);
    }
}
