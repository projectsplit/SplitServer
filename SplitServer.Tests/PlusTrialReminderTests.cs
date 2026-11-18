using SplitServer.Models;
using SplitServer.Services.Plus;

namespace SplitServer.Tests;

/// <summary>
/// The two notices that an account's free Plus days are ending: when each is due, and what it says.
/// They are the only warning anyone gets before features they have been using lock, so the timing
/// is pinned edge by edge, and so are the words — a notice that reads as "Buqs is about to cost
/// money" would be untrue and would lose people who had no reason to leave.
/// </summary>
public class PlusTrialReminderTests
{
    private static readonly DateTime Now = new(2027, 3, 10, 12, 0, 0, DateTimeKind.Utc);

    private const int ReminderDays = 7;
    private const int FinalDays = 1;

    private static PlusAccess Trial(TimeSpan left) =>
        new() { Kind = PlusAccessKind.Trial, TrialEndsAt = Now + left, Subscription = null };

    private static PlusAccess Of(PlusAccessKind kind) =>
        new()
        {
            Kind = kind,
            TrialEndsAt = kind == PlusAccessKind.None ? Now.AddDays(-3) : null,
            Subscription = kind == PlusAccessKind.Subscription
                ? new UserPlus
                {
                    Plan = PlusPlan.Monthly,
                    ExpiresAt = Now.AddDays(20),
                    AutoRenewing = true,
                    State = "SUBSCRIPTION_STATE_ACTIVE",
                    SubscriptionId = "token-1",
                    RenewalPrice = 2.99m,
                    RenewalCurrency = "GBP",
                    Since = Now.AddDays(-10),
                }
                : null,
        };

    private static PlusTrialReminderAction Decide(PlusAccess access, int stage, int reminderDays = ReminderDays, int finalDays = FinalDays) =>
        PlusTrialReminder.Decide(access, stage, Now, reminderDays, finalDays);

    // ---- When ---------------------------------------------------------------------------------

    [Fact]
    public void Nothing_is_said_while_the_end_is_still_far_off()
    {
        Assert.Equal(PlusTrialReminderAction.None, Decide(Trial(TimeSpan.FromDays(20)), stage: 0));
        Assert.Equal(PlusTrialReminderAction.None, Decide(Trial(TimeSpan.FromDays(7).Add(TimeSpan.FromMinutes(1))), stage: 0));
    }

    [Fact]
    public void The_first_notice_goes_out_a_week_before_the_end()
    {
        Assert.Equal(PlusTrialReminderAction.SendFirst, Decide(Trial(TimeSpan.FromDays(7)), stage: 0));
        Assert.Equal(PlusTrialReminderAction.SendFirst, Decide(Trial(TimeSpan.FromDays(6.9)), stage: 0));
        Assert.Equal(PlusTrialReminderAction.SendFirst, Decide(Trial(TimeSpan.FromDays(3)), stage: 0));
    }

    [Fact]
    public void The_first_notice_goes_out_once()
    {
        Assert.Equal(PlusTrialReminderAction.None, Decide(Trial(TimeSpan.FromDays(6)), stage: 1));
        Assert.Equal(PlusTrialReminderAction.None, Decide(Trial(TimeSpan.FromDays(1).Add(TimeSpan.FromMinutes(1))), stage: 1));
    }

    [Fact]
    public void The_last_notice_goes_out_a_day_before_the_end()
    {
        Assert.Equal(PlusTrialReminderAction.SendFinal, Decide(Trial(TimeSpan.FromDays(1)), stage: 1));
        Assert.Equal(PlusTrialReminderAction.SendFinal, Decide(Trial(TimeSpan.FromHours(5)), stage: 1));
        Assert.Equal(PlusTrialReminderAction.SendFinal, Decide(Trial(TimeSpan.FromSeconds(1)), stage: 1));
    }

    /// <summary>
    /// An account that comes into view already inside the last day — the worker was down for a week
    /// — gets the one notice that is still true, not both in the same minute.
    /// </summary>
    [Fact]
    public void An_account_that_missed_the_first_notice_gets_only_the_last()
    {
        Assert.Equal(PlusTrialReminderAction.SendFinal, Decide(Trial(TimeSpan.FromHours(20)), stage: 0));
    }

    [Fact]
    public void Nothing_more_is_said_once_the_last_notice_has_gone()
    {
        Assert.Equal(PlusTrialReminderAction.None, Decide(Trial(TimeSpan.FromHours(20)), stage: 2));
        Assert.Equal(PlusTrialReminderAction.None, Decide(Trial(TimeSpan.FromDays(5)), stage: 2));
    }

    /// <summary>
    /// Their free days ending changes nothing for them. They are marked rather than merely skipped,
    /// so that they stop being fetched on every pass for ever.
    /// </summary>
    [Theory]
    [InlineData(PlusAccessKind.Subscription)]
    [InlineData(PlusAccessKind.Lifetime)]
    public void An_account_that_has_Plus_another_way_is_never_told_and_is_set_aside(PlusAccessKind kind)
    {
        Assert.Equal(PlusTrialReminderAction.MarkDone, Decide(Of(kind), stage: 0));
        Assert.Equal(PlusTrialReminderAction.MarkDone, Decide(Of(kind), stage: 1));
        Assert.Equal(PlusTrialReminderAction.None, Decide(Of(kind), stage: 2));
    }

    /// <summary>A warning after the fact is not a warning.</summary>
    [Fact]
    public void An_account_whose_free_days_are_already_over_is_never_told_and_is_set_aside()
    {
        Assert.Equal(PlusTrialReminderAction.MarkDone, Decide(Of(PlusAccessKind.None), stage: 0));
        Assert.Equal(PlusTrialReminderAction.MarkDone, Decide(Of(PlusAccessKind.None), stage: 1));
        Assert.Equal(PlusTrialReminderAction.None, Decide(Of(PlusAccessKind.None), stage: 2));
    }

    /// <summary>
    /// Not marked either. If Plus goes live later, these accounts start their free days then, and
    /// must still be owed both notices.
    /// </summary>
    [Fact]
    public void Nothing_is_said_or_recorded_while_Plus_is_not_live()
    {
        Assert.Equal(PlusTrialReminderAction.None, Decide(Of(PlusAccessKind.Open), stage: 0));
    }

    [Fact]
    public void The_first_notice_can_be_switched_off_on_its_own()
    {
        Assert.Equal(PlusTrialReminderAction.None, Decide(Trial(TimeSpan.FromDays(5)), stage: 0, reminderDays: 0));
        Assert.Equal(PlusTrialReminderAction.SendFinal, Decide(Trial(TimeSpan.FromHours(20)), stage: 0, reminderDays: 0));
    }

    [Fact]
    public void The_last_notice_can_be_switched_off_on_its_own()
    {
        Assert.Equal(PlusTrialReminderAction.SendFirst, Decide(Trial(TimeSpan.FromHours(20)), stage: 0, finalDays: 0));
        Assert.Equal(PlusTrialReminderAction.None, Decide(Trial(TimeSpan.FromHours(20)), stage: 1, finalDays: 0));
    }

    // ---- What it says -------------------------------------------------------------------------

    private const string WhatChanges =
        "After that, budgeting, analytics, search, places and payments from notifications need a subscription. " +
        "The rest of Buqs stays free.";

    [Fact]
    public void The_first_notice_names_the_day_the_free_days_end()
    {
        var endsAt = new DateTime(2027, 3, 17, 10, 0, 0, DateTimeKind.Utc);

        Assert.Equal("Your free Plus days end on 17 March", PlusTrialReminder.FirstTitle(endsAt, "Europe/London"));
    }

    /// <summary>
    /// Late in the evening UTC is already the next day in Tokyo, and the day named should be the
    /// one the reader will see it happen on.
    /// </summary>
    [Fact]
    public void The_day_is_the_one_on_the_readers_own_calendar()
    {
        var endsAt = new DateTime(2027, 3, 17, 23, 30, 0, DateTimeKind.Utc);

        Assert.Equal("Your free Plus days end on 18 March", PlusTrialReminder.FirstTitle(endsAt, "Asia/Tokyo"));
        Assert.Equal("Your free Plus days end on 17 March", PlusTrialReminder.FirstTitle(endsAt, "America/New_York"));
        Assert.Equal("Your free Plus days end on 17 March", PlusTrialReminder.FirstTitle(endsAt, null));
        Assert.Equal("Your free Plus days end on 17 March", PlusTrialReminder.FirstTitle(endsAt, "Nowhere/Land"));
    }

    [Fact]
    public void The_first_notice_says_how_long_is_left_what_changes_and_what_does_not()
    {
        Assert.Equal(
            $"You have 7 days left of Buqs Plus. {WhatChanges}",
            PlusTrialReminder.FirstBody(Now.AddDays(7), Now));
    }

    /// <summary>
    /// Sent by an hourly pass, the notice goes out with a little under seven days left. That is
    /// "7 days" to the person reading it, not 6.
    /// </summary>
    [Theory]
    [InlineData(7 * 24, 7)]
    [InlineData((7 * 24) - 1, 7)]
    [InlineData((6 * 24) + 1, 7)]
    [InlineData(6 * 24, 6)]
    [InlineData(49, 3)]
    [InlineData(48, 2)]
    public void Part_of_a_day_counts_as_a_day_left(int hoursLeft, int expectedDays)
    {
        Assert.Equal(expectedDays, PlusTrialReminder.DaysLeft(Now.AddHours(hoursLeft), Now));
        Assert.StartsWith($"You have {expectedDays} days left of Buqs Plus.", PlusTrialReminder.FirstBody(Now.AddHours(hoursLeft), Now));
    }

    [Theory]
    [InlineData(24)]
    [InlineData(5)]
    public void One_day_is_a_day_not_days(int hoursLeft)
    {
        Assert.Equal($"You have 1 day left of Buqs Plus. {WhatChanges}", PlusTrialReminder.FirstBody(Now.AddHours(hoursLeft), Now));
    }

    [Fact]
    public void The_last_notice_says_tomorrow_when_it_is_tomorrow()
    {
        // 12:00 UTC now, ending 11:00 UTC the next day: tomorrow in London, and still tomorrow in Tokyo.
        var endsAt = Now.AddHours(23);

        Assert.Equal("Your free Plus days end tomorrow", PlusTrialReminder.FinalTitle(endsAt, Now, "Europe/London"));
        Assert.Equal("Your free Plus days end tomorrow", PlusTrialReminder.FinalTitle(endsAt, Now, "Asia/Tokyo"));
        Assert.Equal("Your free Plus days end tomorrow", PlusTrialReminder.FinalTitle(endsAt, Now, null));
    }

    /// <summary>
    /// A notice that goes out late must not promise a day more than there is.
    /// </summary>
    [Fact]
    public void The_last_notice_says_today_when_it_went_out_late()
    {
        var endsAt = Now.AddHours(6);

        Assert.Equal("Your free Plus days end today", PlusTrialReminder.FinalTitle(endsAt, Now, "Europe/London"));
    }

    /// <summary>
    /// The same moment is today for one reader and tomorrow for another. 12:00 UTC is 21:00 in
    /// Tokyo, so six hours on is past their midnight.
    /// </summary>
    [Fact]
    public void Today_and_tomorrow_are_the_readers_own()
    {
        var endsAt = Now.AddHours(6);

        Assert.Equal("Your free Plus days end today", PlusTrialReminder.FinalTitle(endsAt, Now, "Europe/London"));
        Assert.Equal("Your free Plus days end tomorrow", PlusTrialReminder.FinalTitle(endsAt, Now, "Asia/Tokyo"));
    }

    /// <summary>With the last notice set further out than a day, neither word would be true.</summary>
    [Fact]
    public void The_last_notice_names_the_day_when_it_is_neither_today_nor_tomorrow()
    {
        var endsAt = Now.AddDays(3);

        Assert.Equal("Your free Plus days end on 13 March", PlusTrialReminder.FinalTitle(endsAt, Now, "Europe/London"));
    }

    [Fact]
    public void The_last_notice_says_what_changes_and_what_does_not()
    {
        Assert.Equal(WhatChanges, PlusTrialReminder.FinalBody);
    }

    [Fact]
    public void Both_notices_open_the_Plus_sheet()
    {
        Assert.Equal("/?plus=1", PlusTrialReminder.Url);
    }
}
