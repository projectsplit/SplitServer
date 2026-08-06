using SplitServer.Models;
using SplitServer.Services;

namespace SplitServer.Tests;

/// <summary>
/// The schedule maths is where recurring expenses can be quietly wrong for months, so the awkward
/// cases are pinned here: short months, leap years, DST, and the fortnightly phase.
/// </summary>
public class RecurrenceCalculatorTests
{
    private const string Athens = "Europe/Athens";

    private static RecurrenceSchedule Daily(int hour = 9, int minute = 0) => new()
    {
        Frequency = RecurrenceFrequency.Daily,
        Hour = hour,
        Minute = minute
    };

    private static RecurrenceSchedule Weekly(DayOfWeek day, int hour = 9) => new()
    {
        Frequency = RecurrenceFrequency.Weekly,
        Hour = hour,
        Minute = 0,
        DayOfWeek = day
    };

    private static RecurrenceSchedule Biweekly(DayOfWeek day, int hour = 9) => new()
    {
        Frequency = RecurrenceFrequency.Biweekly,
        Hour = hour,
        Minute = 0,
        DayOfWeek = day
    };

    private static RecurrenceSchedule Monthly(int dayOfMonth, int hour = 9) => new()
    {
        Frequency = RecurrenceFrequency.Monthly,
        Hour = hour,
        Minute = 0,
        DayOfMonth = dayOfMonth
    };

    private static RecurrenceSchedule Annually(int month, int dayOfMonth, int hour = 9) => new()
    {
        Frequency = RecurrenceFrequency.Annually,
        Hour = hour,
        Minute = 0,
        Month = month,
        DayOfMonth = dayOfMonth
    };

    /// <summary>Athens local wall clock time expressed as the UTC instant it refers to.</summary>
    private static DateTime AthensLocal(int year, int month, int day, int hour, int minute = 0)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(Athens);
        var local = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    [Fact]
    public void First_daily_occurrence_is_today_when_the_time_has_not_passed()
    {
        var now = AthensLocal(2026, 3, 10, 7, 30);

        var first = RecurrenceCalculator.GetFirstOccurrence(now, Daily(9), Athens);

        Assert.Equal(AthensLocal(2026, 3, 10, 9), first);
    }

    [Fact]
    public void First_daily_occurrence_rolls_to_tomorrow_when_the_time_has_passed()
    {
        var now = AthensLocal(2026, 3, 10, 9, 30);

        var first = RecurrenceCalculator.GetFirstOccurrence(now, Daily(9), Athens);

        Assert.Equal(AthensLocal(2026, 3, 11, 9), first);
    }

    [Fact]
    public void A_slot_in_the_current_minute_starts_now_rather_than_a_cycle_later()
    {
        // Picking the time it already is means "start now". The seconds that have elapsed within
        // that minute must not push the whole series to tomorrow.
        var now = AthensLocal(2026, 3, 10, 9).AddSeconds(40);

        var first = RecurrenceCalculator.GetFirstOccurrence(now, Daily(9), Athens);

        Assert.Equal(AthensLocal(2026, 3, 10, 9), first);
    }

    [Fact]
    public void A_weekly_slot_in_the_current_minute_starts_today()
    {
        // 10 March 2026 is a Tuesday, and the schedule is for Tuesdays. Setting it up during its
        // own minute should start today, not defer a week.
        var now = AthensLocal(2026, 3, 10, 9).AddSeconds(40);

        var first = RecurrenceCalculator.GetFirstOccurrence(now, Weekly(DayOfWeek.Tuesday), Athens);

        Assert.Equal(AthensLocal(2026, 3, 10, 9), first);
    }

    [Fact]
    public void A_slot_a_minute_ago_still_defers()
    {
        var now = AthensLocal(2026, 3, 10, 9, 1);

        var first = RecurrenceCalculator.GetFirstOccurrence(now, Daily(9), Athens);

        Assert.Equal(AthensLocal(2026, 3, 11, 9), first);
    }

    [Fact]
    public void First_weekly_occurrence_lands_on_the_chosen_weekday()
    {
        // 10 March 2026 is a Tuesday.
        var now = AthensLocal(2026, 3, 10, 12);

        var first = RecurrenceCalculator.GetFirstOccurrence(now, Weekly(DayOfWeek.Friday), Athens);

        Assert.Equal(AthensLocal(2026, 3, 13, 9), first);
        Assert.Equal(DayOfWeek.Friday, ToLocal(first).DayOfWeek);
    }

    [Fact]
    public void First_biweekly_occurrence_is_one_week_out_not_two()
    {
        // Set up on a Tuesday after that day's slot. The fortnightly spacing is measured from the
        // first occurrence, so it should start next Tuesday rather than skipping a fortnight.
        var now = AthensLocal(2026, 3, 10, 12);

        var first = RecurrenceCalculator.GetFirstOccurrence(now, Biweekly(DayOfWeek.Tuesday), Athens);

        Assert.Equal(AthensLocal(2026, 3, 17, 9), first);
    }

    [Fact]
    public void Biweekly_spaces_by_fourteen_days_after_the_first_occurrence()
    {
        var first = AthensLocal(2026, 3, 17, 9);

        var next = RecurrenceCalculator.GetNext(first, Biweekly(DayOfWeek.Tuesday), Athens);

        Assert.Equal(AthensLocal(2026, 3, 31, 9), next);
    }

    [Fact]
    public void Monthly_clamps_to_the_last_day_of_a_short_month()
    {
        var january = AthensLocal(2026, 1, 31, 9);

        var february = RecurrenceCalculator.GetNext(january, Monthly(31), Athens);

        Assert.Equal(AthensLocal(2026, 2, 28, 9), february);
    }

    [Fact]
    public void Monthly_returns_to_the_chosen_day_after_clamping()
    {
        // The regression this guards: stepping from the clamped date instead of the schedule would
        // leave every later month stuck on the 28th.
        var february = AthensLocal(2026, 2, 28, 9);

        var march = RecurrenceCalculator.GetNext(february, Monthly(31), Athens);

        Assert.Equal(AthensLocal(2026, 3, 31, 9), march);
    }

    [Fact]
    public void Monthly_rolls_over_the_year_boundary()
    {
        var december = AthensLocal(2026, 12, 15, 9);

        var january = RecurrenceCalculator.GetNext(december, Monthly(15), Athens);

        Assert.Equal(AthensLocal(2027, 1, 15, 9), january);
    }

    [Fact]
    public void Annual_29_february_clamps_in_a_non_leap_year_and_recovers_in_the_next_leap_year()
    {
        var leap = AthensLocal(2024, 2, 29, 9);

        var nonLeap = RecurrenceCalculator.GetNext(leap, Annually(2, 29), Athens);
        Assert.Equal(AthensLocal(2025, 2, 28, 9), nonLeap);

        var stillNonLeap = RecurrenceCalculator.GetNext(nonLeap, Annually(2, 29), Athens);
        Assert.Equal(AthensLocal(2026, 2, 28, 9), stillNonLeap);

        var backToLeap = RecurrenceCalculator.GetNext(
            RecurrenceCalculator.GetNext(stillNonLeap, Annually(2, 29), Athens),
            Annually(2, 29),
            Athens);
        Assert.Equal(AthensLocal(2028, 2, 29, 9), backToLeap);
    }

    [Fact]
    public void The_wall_clock_time_survives_a_dst_transition()
    {
        // Athens moves to summer time on 29 March 2026. A daily 09:00 has to stay 09:00 local,
        // which means the UTC instant shifts by an hour.
        var beforeTransition = AthensLocal(2026, 3, 28, 9);

        var afterTransition = RecurrenceCalculator.GetNext(
            RecurrenceCalculator.GetNext(beforeTransition, Daily(), Athens),
            Daily(),
            Athens);

        Assert.Equal(9, ToLocal(afterTransition).Hour);
        Assert.Equal(new DateTime(2026, 3, 30), ToLocal(afterTransition).Date);
    }

    [Fact]
    public void Catch_up_skips_missed_occurrences_instead_of_replaying_them()
    {
        var missedSince = AthensLocal(2026, 3, 1, 9);
        var now = AthensLocal(2026, 3, 10, 12);

        var (next, skipped) = RecurrenceCalculator.CatchUp(missedSince, now, Daily(), Athens);

        Assert.Equal(AthensLocal(2026, 3, 11, 9), next);
        Assert.True(skipped > 0);
    }

    [Fact]
    public void Catch_up_on_a_schedule_that_is_only_just_due_skips_nothing()
    {
        var due = AthensLocal(2026, 3, 10, 9);
        var now = AthensLocal(2026, 3, 10, 9, 1);

        var (next, skipped) = RecurrenceCalculator.CatchUp(due, now, Daily(), Athens);

        Assert.Equal(AthensLocal(2026, 3, 11, 9), next);
        Assert.Equal(0, skipped);
    }

    [Fact]
    public void An_unknown_time_zone_still_produces_a_usable_schedule()
    {
        var now = new DateTime(2026, 3, 10, 7, 0, 0, DateTimeKind.Utc);

        var first = RecurrenceCalculator.GetFirstOccurrence(now, Daily(9), "Not/A_Zone");

        Assert.Equal(new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc), first);
    }

    [Theory]
    [InlineData(RecurrenceFrequency.Weekly)]
    [InlineData(RecurrenceFrequency.Biweekly)]
    public void A_weekday_is_required_for_weekly_and_biweekly(RecurrenceFrequency frequency)
    {
        var result = RecurrenceCalculator.Validate(
            new RecurrenceSchedule { Frequency = frequency, Hour = 9, Minute = 0 });

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void A_day_is_required_for_monthly()
    {
        var result = RecurrenceCalculator.Validate(
            new RecurrenceSchedule { Frequency = RecurrenceFrequency.Monthly, Hour = 9, Minute = 0 });

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void A_month_and_day_are_required_for_annually()
    {
        var result = RecurrenceCalculator.Validate(
            new RecurrenceSchedule { Frequency = RecurrenceFrequency.Annually, Hour = 9, Minute = 0 });

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void The_31st_of_february_is_rejected_but_the_29th_is_accepted()
    {
        Assert.True(RecurrenceCalculator.Validate(Annually(2, 31)).IsFailure);
        Assert.True(RecurrenceCalculator.Validate(Annually(2, 29)).IsSuccess);
    }

    [Fact]
    public void A_missing_schedule_is_rejected_rather_than_throwing()
    {
        // A stored document can come back without one. Validation has to say so, because the
        // alternative is a null dereference inside the worker that aborts the whole pass.
        var result = RecurrenceCalculator.Validate(null);

        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(24, 0)]
    [InlineData(9, -1)]
    [InlineData(9, 60)]
    public void An_out_of_range_time_is_rejected(int hour, int minute)
    {
        var result = RecurrenceCalculator.Validate(
            new RecurrenceSchedule { Frequency = RecurrenceFrequency.Daily, Hour = hour, Minute = minute });

        Assert.True(result.IsFailure);
    }

    private static DateTime ToLocal(DateTime utc)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById(Athens));
    }
}
