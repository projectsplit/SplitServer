using SplitServer.Models;
using SplitServer.Responses;
using SplitServer.Services.Plus;

namespace SplitServer.Tests;

/// <summary>
/// Who has Buqs Plus, and when a subscription wants looking at again. These are the rules that
/// decide whether somebody who paid gets what they paid for, and whether somebody who did not goes
/// on getting it anyway — and both mistakes are silent. Nothing crashes when a state is put in the
/// wrong list; a person simply has, or lacks, Plus. So each state and each edge of each date is
/// pinned on its own.
/// </summary>
public class PlusEntitlementTests
{
    private static readonly DateTime Now = new(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);

    private const string Active = "SUBSCRIPTION_STATE_ACTIVE";
    private const string InGracePeriod = "SUBSCRIPTION_STATE_IN_GRACE_PERIOD";
    private const string Canceled = "SUBSCRIPTION_STATE_CANCELED";
    private const string OnHold = "SUBSCRIPTION_STATE_ON_HOLD";
    private const string Paused = "SUBSCRIPTION_STATE_PAUSED";
    private const string Pending = "SUBSCRIPTION_STATE_PENDING";
    private const string Expired = "SUBSCRIPTION_STATE_EXPIRED";

    private static PlusSubscription Subscription(
        string state = Active,
        DateTime? expiresAt = null,
        bool autoRenewing = true,
        PlusPlan plan = PlusPlan.Annual,
        DateTime? updated = null,
        DateTime? reminderSentFor = null,
        string id = "token-1",
        bool noExpiry = false) =>
        new()
        {
            Id = id,
            Created = Now.AddDays(-40),
            Updated = updated ?? Now.AddHours(-1),
            UserId = "user-1",
            ProductId = "buqs_plus",
            BasePlanId = plan == PlusPlan.Annual ? "annual" : "monthly",
            Plan = plan,
            State = state,
            ExpiresAt = noExpiry ? null : expiresAt ?? Now.AddDays(30),
            AutoRenewing = autoRenewing,
            RenewalPrice = 19.99m,
            RenewalCurrency = "GBP",
            LatestOrderId = "GPA.1234",
            StartedAt = Now.AddDays(-40),
            RenewalReminderSentFor = reminderSentFor,
        };

    // ---- Entitlement ------------------------------------------------------------------------

    [Theory]
    [InlineData(Active)]
    [InlineData(InGracePeriod)]
    // Cancelling stops the next payment, not the time already bought.
    [InlineData(Canceled)]
    public void Gives_Plus_while_the_paid_period_is_still_running(string state)
    {
        Assert.True(PlusEntitlement.IsEntitled(state, Now.AddDays(1), autoRenewing: false, Now));
    }

    [Theory]
    // No money is covering today in any of these, whatever the date on the row says.
    [InlineData(OnHold)]
    [InlineData(Paused)]
    [InlineData(Pending)]
    [InlineData(Expired)]
    [InlineData(PlusEntitlement.Replaced)]
    [InlineData("SUBSCRIPTION_STATE_PENDING_PURCHASE_CANCELED")]
    [InlineData("SUBSCRIPTION_STATE_UNSPECIFIED")]
    [InlineData("")]
    public void Gives_nothing_in_a_state_that_is_not_paying(string state)
    {
        Assert.False(PlusEntitlement.IsEntitled(state, Now.AddDays(30), autoRenewing: true, Now));
    }

    [Fact]
    public void Gives_nothing_without_an_expiry_to_hold_it_to()
    {
        Assert.False(PlusEntitlement.IsEntitled(Active, expiresAt: null, autoRenewing: true, Now));
    }

    [Fact]
    public void A_cancelled_plan_ends_exactly_when_its_period_does()
    {
        Assert.True(PlusEntitlement.IsEntitled(Canceled, Now.AddSeconds(1), autoRenewing: false, Now));
        Assert.False(PlusEntitlement.IsEntitled(Canceled, Now, autoRenewing: false, Now));
        Assert.False(PlusEntitlement.IsEntitled(Canceled, Now.AddMinutes(-1), autoRenewing: false, Now));
    }

    /// <summary>
    /// The row's date is the end of the period that was paid for when the row was last read. Play
    /// renews at that instant and this server hears about it later, so a subscriber who paid on time
    /// must not lose Plus for the gap in between.
    /// </summary>
    [Fact]
    public void A_plan_that_is_due_to_renew_keeps_counting_while_the_renewal_is_being_confirmed()
    {
        var justRolledOver = Now.AddMinutes(-90);

        Assert.True(PlusEntitlement.IsEntitled(Active, justRolledOver, autoRenewing: true, Now));
    }

    [Fact]
    public void The_allowance_for_a_renewal_runs_out()
    {
        var longOverdue = Now - PlusEntitlement.RenewalLeeway;

        Assert.False(PlusEntitlement.IsEntitled(Active, longOverdue, autoRenewing: true, Now));
        Assert.True(PlusEntitlement.IsEntitled(Active, longOverdue.AddSeconds(1), autoRenewing: true, Now));
    }

    [Theory]
    // Renewal was turned off, so for them the date really is the end.
    [InlineData(Active, false)]
    [InlineData(Canceled, false)]
    [InlineData(Canceled, true)]
    // Already past its renewal date and failing; its expiry is the end of the grace Play gave it.
    [InlineData(InGracePeriod, true)]
    public void The_allowance_is_only_for_an_active_plan_that_will_renew(string state, bool autoRenewing)
    {
        Assert.False(PlusEntitlement.IsEntitled(state, Now.AddMinutes(-5), autoRenewing, Now));
    }

    [Fact]
    public void A_user_with_no_mark_does_not_have_Plus()
    {
        Assert.False(PlusEntitlement.IsEntitled((UserPlus?)null, Now));
    }

    [Fact]
    public void A_mark_left_behind_after_its_period_counts_for_nothing()
    {
        var stale = PlusEntitlement.ToUserPlus(Subscription(state: Canceled, autoRenewing: false, expiresAt: Now.AddDays(-2)));

        Assert.NotNull(stale);
        Assert.False(PlusEntitlement.IsEntitled(stale, Now));
        Assert.Null(PlusMembershipResponse.From(stale, Now));
    }

    // ---- Which subscription is the one that counts -------------------------------------------

    [Fact]
    public void Nobody_has_Plus_from_subscriptions_that_have_all_run_out()
    {
        var subscriptions = new[]
        {
            Subscription(state: Expired, expiresAt: Now.AddDays(-10), id: "old"),
            Subscription(state: OnHold, expiresAt: Now.AddDays(-1), id: "held"),
        };

        Assert.Null(PlusEntitlement.Current(subscriptions, Now));
        Assert.Null(PlusEntitlement.ToUserPlus(PlusEntitlement.Current(subscriptions, Now)));
    }

    /// <summary>
    /// Resubscribing leaves an old row ending and a new one beginning. Whichever is processed last,
    /// the answer has to be the same, which is why it is taken across all of them.
    /// </summary>
    [Fact]
    public void The_subscription_that_lasts_longest_is_the_one_that_counts()
    {
        var ending = Subscription(state: Canceled, autoRenewing: false, expiresAt: Now.AddDays(3), plan: PlusPlan.Monthly, id: "ending");
        var replacement = Subscription(expiresAt: Now.AddDays(365), plan: PlusPlan.Annual, id: "replacement");
        var lapsed = Subscription(state: Expired, expiresAt: Now.AddDays(-100), id: "lapsed");

        Assert.Equal("replacement", PlusEntitlement.Current([ending, replacement, lapsed], Now)?.Id);
        Assert.Equal("replacement", PlusEntitlement.Current([lapsed, replacement, ending], Now)?.Id);
    }

    [Fact]
    public void The_mark_on_the_user_is_a_copy_of_the_subscription_that_counts()
    {
        var subscription = Subscription(expiresAt: Now.AddDays(200));

        var mark = PlusEntitlement.ToUserPlus(subscription);

        Assert.NotNull(mark);
        Assert.Equal(PlusPlan.Annual, mark.Plan);
        Assert.Equal(Now.AddDays(200), mark.ExpiresAt);
        Assert.True(mark.AutoRenewing);
        Assert.Equal(Active, mark.State);
        Assert.Equal("token-1", mark.SubscriptionId);
        Assert.Equal(19.99m, mark.RenewalPrice);
        Assert.Equal("GBP", mark.RenewalCurrency);
        Assert.Equal(Now.AddDays(-40), mark.Since);
    }

    // ---- What the app is told ----------------------------------------------------------------

    [Fact]
    public void The_app_is_told_the_plan_by_name()
    {
        var annual = PlusMembershipResponse.From(PlusEntitlement.ToUserPlus(Subscription(plan: PlusPlan.Annual)), Now);
        var monthly = PlusMembershipResponse.From(PlusEntitlement.ToUserPlus(Subscription(plan: PlusPlan.Monthly)), Now);

        Assert.Equal("annual", annual?.Plan);
        Assert.Equal("monthly", monthly?.Plan);
    }

    [Fact]
    public void The_app_is_told_when_a_renewal_payment_is_failing()
    {
        var failing = PlusMembershipResponse.From(
            PlusEntitlement.ToUserPlus(Subscription(state: InGracePeriod, expiresAt: Now.AddDays(2))),
            Now);

        var healthy = PlusMembershipResponse.From(PlusEntitlement.ToUserPlus(Subscription()), Now);

        Assert.True(failing?.InGracePeriod);
        Assert.False(healthy?.InGracePeriod);
    }

    [Fact]
    public void The_app_is_told_what_the_renewal_will_cost_and_when()
    {
        var membership = PlusMembershipResponse.From(
            PlusEntitlement.ToUserPlus(Subscription(expiresAt: Now.AddDays(200))),
            Now);

        Assert.NotNull(membership);
        Assert.Equal(Now.AddDays(200), membership.ExpiresAt);
        Assert.True(membership.AutoRenewing);
        Assert.Equal(19.99m, membership.RenewalPrice);
        Assert.Equal("GBP", membership.RenewalCurrencyCode);
    }

    // ---- When to ask Play again --------------------------------------------------------------

    [Fact]
    public void A_fresh_subscription_inside_its_period_is_left_alone()
    {
        Assert.False(PlusEntitlement.IsRefreshDue(Subscription(updated: Now.AddHours(-23)), Now));
    }

    /// <summary>It has either renewed or lapsed, and the row says neither.</summary>
    [Fact]
    public void A_subscription_whose_period_has_run_out_is_asked_about()
    {
        Assert.True(PlusEntitlement.IsRefreshDue(Subscription(expiresAt: Now, updated: Now.AddMinutes(-5)), Now));
        Assert.True(PlusEntitlement.IsRefreshDue(Subscription(expiresAt: Now.AddDays(-3), updated: Now.AddMinutes(-5)), Now));
    }

    /// <summary>
    /// A refund changes nothing the row could have predicted. With no notification from Play, the
    /// daily look is the only way one is ever noticed.
    /// </summary>
    [Fact]
    public void A_subscription_nobody_has_looked_at_for_a_day_is_asked_about()
    {
        Assert.True(PlusEntitlement.IsRefreshDue(Subscription(updated: Now.AddHours(-24)), Now));
        Assert.True(PlusEntitlement.IsRefreshDue(Subscription(state: OnHold, updated: Now.AddDays(-2)), Now));
    }

    /// <summary>
    /// A payment by a slow method. The person has nothing until it lands, so it is looked for on
    /// every pass rather than left to the daily one.
    /// </summary>
    [Fact]
    public void A_purchase_whose_payment_is_still_on_its_way_is_asked_about_every_pass()
    {
        Assert.True(PlusEntitlement.IsRefreshDue(Subscription(state: Pending, noExpiry: true, updated: Now.AddMinutes(-5)), Now));
        Assert.True(PlusEntitlement.IsRefreshDue(Subscription(state: Pending, expiresAt: Now.AddDays(30), updated: Now.AddMinutes(-5)), Now));
    }

    [Fact]
    public void A_subscription_with_no_dates_that_is_not_pending_waits_for_the_daily_look()
    {
        Assert.False(PlusEntitlement.IsRefreshDue(Subscription(state: Paused, noExpiry: true, updated: Now.AddHours(-2)), Now));
        Assert.True(PlusEntitlement.IsRefreshDue(Subscription(state: Paused, noExpiry: true, updated: Now.AddHours(-25)), Now));
    }

    [Theory]
    [InlineData(Active, true)]
    [InlineData(InGracePeriod, true)]
    [InlineData(Canceled, true)]
    [InlineData(Pending, false)]
    [InlineData(OnHold, false)]
    [InlineData(Paused, false)]
    [InlineData(Expired, false)]
    [InlineData(PlusEntitlement.Replaced, false)]
    public void Only_a_purchase_Play_has_been_paid_for_counts_as_paid(string state, bool expected)
    {
        Assert.Equal(expected, PlusEntitlement.IsPaidFor(state));
    }

    [Theory]
    [InlineData(Expired)]
    [InlineData(PlusEntitlement.Replaced)]
    [InlineData("SUBSCRIPTION_STATE_PENDING_PURCHASE_CANCELED")]
    public void A_finished_subscription_is_never_asked_about_again(string state)
    {
        var finished = Subscription(state: state, expiresAt: Now.AddDays(-400), updated: Now.AddDays(-400));

        Assert.True(PlusEntitlement.HasEnded(state));
        Assert.False(PlusEntitlement.IsRefreshDue(finished, Now));
    }

    // ---- When to say a renewal is coming -----------------------------------------------------

    private const int ReminderDays = 7;

    [Fact]
    public void An_annual_plan_about_to_renew_is_announced()
    {
        Assert.True(PlusEntitlement.IsReminderDue(Subscription(expiresAt: Now.AddDays(6)), Now, ReminderDays));
        Assert.True(PlusEntitlement.IsReminderDue(Subscription(expiresAt: Now.AddDays(7)), Now, ReminderDays));
        Assert.True(PlusEntitlement.IsReminderDue(Subscription(expiresAt: Now.AddMinutes(1)), Now, ReminderDays));
    }

    [Fact]
    public void A_renewal_that_is_still_far_off_is_not_announced_yet()
    {
        Assert.False(PlusEntitlement.IsReminderDue(Subscription(expiresAt: Now.AddDays(7).AddSeconds(1)), Now, ReminderDays));
        Assert.False(PlusEntitlement.IsReminderDue(Subscription(expiresAt: Now.AddDays(200)), Now, ReminderDays));
    }

    [Fact]
    public void A_renewal_that_has_already_happened_is_not_announced()
    {
        Assert.False(PlusEntitlement.IsReminderDue(Subscription(expiresAt: Now), Now, ReminderDays));
        Assert.False(PlusEntitlement.IsReminderDue(Subscription(expiresAt: Now.AddMinutes(-30)), Now, ReminderDays));
    }

    /// <summary>A notice every four weeks is noise, not a warning.</summary>
    [Fact]
    public void A_monthly_plan_is_never_announced()
    {
        Assert.False(PlusEntitlement.IsReminderDue(Subscription(plan: PlusPlan.Monthly, expiresAt: Now.AddDays(3)), Now, ReminderDays));
    }

    [Fact]
    public void A_plan_with_renewal_turned_off_is_never_announced()
    {
        var cancelled = Subscription(state: Canceled, autoRenewing: false, expiresAt: Now.AddDays(3));
        var switchedOff = Subscription(state: Active, autoRenewing: false, expiresAt: Now.AddDays(3));

        Assert.False(PlusEntitlement.IsReminderDue(cancelled, Now, ReminderDays));
        Assert.False(PlusEntitlement.IsReminderDue(switchedOff, Now, ReminderDays));
    }

    /// <summary>
    /// In its grace period a plan is already past its renewal date, and the date on the row is when
    /// Play gives up. "Renews soon" would be wrong twice over.
    /// </summary>
    [Theory]
    [InlineData(InGracePeriod)]
    [InlineData(OnHold)]
    [InlineData(Paused)]
    [InlineData(Expired)]
    public void Only_a_healthy_plan_is_announced(string state)
    {
        Assert.False(PlusEntitlement.IsReminderDue(Subscription(state: state, expiresAt: Now.AddDays(2)), Now, ReminderDays));
    }

    [Fact]
    public void A_renewal_is_announced_once()
    {
        var renewsAt = Now.AddDays(5);

        Assert.False(PlusEntitlement.IsReminderDue(Subscription(expiresAt: renewsAt, reminderSentFor: renewsAt), Now, ReminderDays));
    }

    /// <summary>
    /// What was announced is recorded as the date it was for, not as a plain "sent", so that the
    /// reminder comes round again a year later when the date has moved on.
    /// </summary>
    [Fact]
    public void Next_years_renewal_is_announced_even_though_last_years_was()
    {
        var lastYear = Now.AddDays(5).AddYears(-1);

        Assert.True(PlusEntitlement.IsReminderDue(Subscription(expiresAt: Now.AddDays(5), reminderSentFor: lastYear), Now, ReminderDays));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Reminders_can_be_turned_off(int reminderDays)
    {
        Assert.False(PlusEntitlement.IsReminderDue(Subscription(expiresAt: Now.AddHours(1)), Now, reminderDays));
    }
}
