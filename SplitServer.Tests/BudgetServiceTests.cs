using SplitServer.Models;
using SplitServer.Services;

namespace SplitServer.Tests;

/// <summary>
/// A budget cycle is a run of calendar days in the user's own zone, but expenses are stored as UTC
/// instants. Getting that conversion wrong is invisible for most of the day and only shows up around
/// the boundary, so the offset is pinned here.
/// </summary>
public class BudgetServiceTests
{
    private const string Athens = "Europe/Athens";
    private const string Utc = "UTC";

    // Cuba springs forward at local midnight, so a cycle can start on a day that has no 00:00.
    private const string Havana = "America/Havana";

    // Only the pure date maths is under test, so the collaborators are never reached.
    private readonly BudgetService _sut = new(null!, null!, null!);

    private static Budget MonthlyBudget(int commencementDay) => new()
    {
        Id = "budget",
        Created = DateTime.UtcNow,
        Updated = DateTime.UtcNow,
        UserId = "user",
        Amount = 500,
        Currency = "EUR",
        Description = "test",
        Frequency = BudgetFrequency.Monthly,
        Scope = BudgetScope.Personal,
        CommencementDay = commencementDay.ToString(),
        IsActive = true
    };

    private static Budget WeeklyBudget(DayOfWeek commencementDay) => MonthlyBudget(1) with
    {
        Frequency = BudgetFrequency.Weekly,
        CommencementDay = commencementDay.ToString()
    };

    private static Budget CustomBudget(DateTime startDate, DateTime endDate) => MonthlyBudget(1) with
    {
        Frequency = BudgetFrequency.Custom,
        CommencementDay = null,
        StartDate = startDate,
        EndDate = endDate
    };

    [Fact]
    public void Monthly_cycle_starts_on_the_commencement_day_at_local_midnight()
    {
        var (startDate, _) = _sut.CalculateDates(MonthlyBudget(22), Athens).Value;

        Assert.Equal(22, startDate.Day);
        Assert.Equal(TimeSpan.Zero, startDate.TimeOfDay);
    }

    [Fact]
    public void Monthly_window_in_utc_refers_to_the_same_local_midnight()
    {
        var budget = MonthlyBudget(22);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(Athens);

        var (localStart, localEnd) = _sut.CalculateDates(budget, Athens).Value;
        var (utcStart, utcEnd) = _sut.CalculateUtcDates(budget, Athens).Value;

        Assert.Equal(localStart, TimeZoneInfo.ConvertTimeFromUtc(utcStart, zone));

        // ToUtc keeps millisecond precision, so the end lands a fraction of a millisecond short of
        // the tick the local window ends on.
        var roundTrippedEnd = TimeZoneInfo.ConvertTimeFromUtc(utcEnd, zone);
        Assert.True(roundTrippedEnd <= localEnd);
        Assert.True(localEnd - roundTrippedEnd < TimeSpan.FromMilliseconds(1));
    }

    /// <summary>
    /// The bug this guards: for a zone ahead of UTC the window used to open only once UTC reached
    /// the local wall clock start, leaving expenses made just after local midnight untracked.
    /// </summary>
    [Fact]
    public void Monthly_window_opens_before_the_naive_wall_clock_start_for_a_zone_ahead_of_utc()
    {
        var budget = MonthlyBudget(22);

        var (localStart, _) = _sut.CalculateDates(budget, Athens).Value;
        var (utcStart, _) = _sut.CalculateUtcDates(budget, Athens).Value;

        Assert.True(utcStart < localStart);
        Assert.Equal(
            TimeZoneInfo.FindSystemTimeZoneById(Athens).GetUtcOffset(utcStart),
            localStart - utcStart);
    }

    [Fact]
    public void Utc_user_sees_no_shift_between_the_local_and_utc_windows()
    {
        var budget = MonthlyBudget(22);

        var (localStart, _) = _sut.CalculateDates(budget, Utc).Value;
        var (utcStart, _) = _sut.CalculateUtcDates(budget, Utc).Value;

        Assert.Equal(localStart, utcStart);
    }

    [Fact]
    public void Weekly_window_in_utc_refers_to_the_same_local_midnight()
    {
        var budget = WeeklyBudget(DayOfWeek.Monday);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(Athens);

        var (localStart, _) = _sut.CalculateDates(budget, Athens).Value;
        var (utcStart, _) = _sut.CalculateUtcDates(budget, Athens).Value;

        Assert.Equal(DayOfWeek.Monday, localStart.DayOfWeek);
        Assert.Equal(localStart, TimeZoneInfo.ConvertTimeFromUtc(utcStart, zone));
    }

    [Fact]
    public void Custom_dates_are_the_users_calendar_days_not_utc_days()
    {
        var budget = CustomBudget(new DateTime(2026, 8, 22), new DateTime(2026, 9, 21));

        var (utcStart, utcEnd) = _sut.CalculateUtcDates(budget, Athens).Value;

        // Athens is UTC+3 in August and September, so both boundaries sit on the preceding UTC day.
        Assert.Equal(new DateTime(2026, 8, 21, 21, 0, 0, DateTimeKind.Utc), utcStart);
        Assert.Equal(new DateTime(2026, 9, 21, 20, 59, 59, 999, DateTimeKind.Utc), utcEnd);
    }

    /// <summary>
    /// A cycle keeps the same start date for its whole run, so a start date the clocks skipped over
    /// would have failed every request for the length of the cycle, not just on the day itself.
    /// </summary>
    [Fact]
    public void Cycle_starting_on_a_skipped_midnight_resolves_to_the_first_instant_of_that_day()
    {
        var budget = CustomBudget(new DateTime(2026, 3, 8), new DateTime(2026, 4, 7));

        var (utcStart, utcEnd) = _sut.CalculateUtcDates(budget, Havana).Value;

        Assert.Equal(new DateTime(2026, 3, 8, 5, 0, 0, DateTimeKind.Utc), utcStart);
        Assert.Equal(new DateTime(2026, 4, 8, 3, 59, 59, 999, DateTimeKind.Utc), utcEnd);
    }

    [Fact]
    public void Monthly_commencement_day_is_capped_to_the_length_of_a_short_month()
    {
        var (startDate, _) = _sut.CalculateDates(MonthlyBudget(31), Athens).Value;

        Assert.Equal(Math.Min(31, DateTime.DaysInMonth(startDate.Year, startDate.Month)), startDate.Day);
    }
}
