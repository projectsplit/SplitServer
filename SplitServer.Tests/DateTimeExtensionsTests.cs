using SplitServer.Extensions;

namespace SplitServer.Tests;

/// <summary>
/// Turning a user's wall clock time into a UTC instant is where a zone's awkward days surface: the
/// hour a spring forward deletes has no instant to convert to, and asking for it used to throw.
/// </summary>
public class DateTimeExtensionsTests
{
    // Cuba springs forward at local midnight, so the day the clocks change has no 00:00 at all.
    private const string Havana = "America/Havana";
    private const string Athens = "Europe/Athens";

    [Fact]
    public void Midnight_that_dst_deleted_resolves_to_the_first_instant_of_that_day()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(Havana);
        var skippedMidnight = new DateTime(2026, 3, 8, 0, 0, 0, DateTimeKind.Unspecified);

        // Pins the premise, so a tzdata change that moves Cuba's transition fails here and says why
        // rather than quietly making the assertion below vacuous.
        Assert.True(zone.IsInvalidTime(skippedMidnight));

        // 00:00 does not exist; 01:00 CDT is the first moment of the 8th, which is 05:00 UTC.
        Assert.Equal(
            new DateTime(2026, 3, 8, 5, 0, 0, DateTimeKind.Utc),
            skippedMidnight.ToUtc(Havana));
    }

    [Fact]
    public void Midnight_on_an_ordinary_day_is_left_alone()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(Havana);
        var midnight = new DateTime(2026, 3, 9, 0, 0, 0, DateTimeKind.Unspecified);

        Assert.False(zone.IsInvalidTime(midnight));

        // The day after the change, so already on summer time at UTC-4.
        Assert.Equal(new DateTime(2026, 3, 9, 4, 0, 0, DateTimeKind.Utc), midnight.ToUtc(Havana));
    }

    [Fact]
    public void Wall_clock_time_is_read_in_the_users_zone_not_as_utc()
    {
        var midnight = new DateTime(2026, 8, 22, 0, 0, 0, DateTimeKind.Unspecified);

        // Athens is UTC+3 in August, so the local day starts on the previous UTC day.
        Assert.Equal(new DateTime(2026, 8, 21, 21, 0, 0, DateTimeKind.Utc), midnight.ToUtc(Athens));
    }
}
