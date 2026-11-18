using SplitServer.Models;
using SplitServer.Services.Plus;

namespace SplitServer.Tests;

/// <summary>
/// What access an account has to the Plus features. This one function decides whether somebody
/// sees a lock, so each of its five answers is pinned, and so is the order they are tried in —
/// because the cases that go wrong are the ones where two of them are true at once.
/// </summary>
public class PlusAccessRuleTests
{
    private static readonly DateTime CutOff = new(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LiveSince = new(2027, 1, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = new(2027, 3, 10, 12, 0, 0, DateTimeKind.Utc);

    private const string Active = "SUBSCRIPTION_STATE_ACTIVE";
    private const string Canceled = "SUBSCRIPTION_STATE_CANCELED";
    private const string Expired = "SUBSCRIPTION_STATE_EXPIRED";

    private static PlusTerms Live(int trialDays = 30, DateTime? lifetimeBefore = null, bool noCutOff = false) =>
        new(IsOffered: true, LiveSince, trialDays, noCutOff ? null : lifetimeBefore ?? CutOff);

    private static PlusTerms NotLive() =>
        new(IsOffered: false, LiveSince: Now, TrialDays: 30, LifetimeBefore: CutOff);

    private static User Account(DateTime created, UserPlus? plus = null, DateTime? plusForLife = null) =>
        new()
        {
            Id = "user-1",
            Created = created,
            Updated = created,
            Email = null,
            EmailVerified = false,
            HashedPassword = null,
            Username = "someone",
            GoogleId = null,
            Plus = plus,
            PlusForLife = plusForLife,
        };

    private static UserPlus Subscribed(DateTime expiresAt, string state = Active, bool autoRenewing = true) =>
        new()
        {
            Plan = PlusPlan.Annual,
            ExpiresAt = expiresAt,
            AutoRenewing = autoRenewing,
            State = state,
            SubscriptionId = "token-1",
            RenewalPrice = 19.99m,
            RenewalCurrency = "GBP",
            Since = expiresAt.AddYears(-1),
        };

    /// <summary>An account made well after both the cut-off and the launch, so neither is what a test is about.</summary>
    private static readonly DateTime Recently = Now.AddDays(-10);

    // ---- Plus is not live -------------------------------------------------------------------

    /// <summary>
    /// Until Plus is switched on nothing is sold and nothing is locked, and that has to hold for
    /// every kind of account — including ones the other rules would have plenty to say about.
    /// </summary>
    [Fact]
    public void Everything_is_open_to_everyone_while_Plus_is_not_live()
    {
        User[] accounts =
        [
            Account(Recently),
            Account(Now.AddDays(-200)),
            Account(CutOff.AddDays(-90)),
            Account(Recently, plusForLife: Now.AddDays(-1)),
            Account(Recently, plus: Subscribed(Now.AddDays(100))),
        ];

        foreach (var account in accounts)
        {
            var access = PlusAccessRule.Evaluate(account, NotLive(), Now);

            Assert.Equal(PlusAccessKind.Open, access.Kind);
            Assert.Null(access.TrialEndsAt);
            Assert.Null(access.Subscription);
            Assert.True(access.HasPlus);
        }
    }

    // ---- Subscription -----------------------------------------------------------------------

    [Fact]
    public void Someone_paying_has_Plus_by_their_subscription()
    {
        var plus = Subscribed(Now.AddDays(100));

        var access = PlusAccessRule.Evaluate(Account(Now.AddDays(-200), plus), Live(), Now);

        Assert.Equal(PlusAccessKind.Subscription, access.Kind);
        Assert.Same(plus, access.Subscription);
        Assert.Null(access.TrialEndsAt);
        Assert.True(access.HasPlus);
    }

    /// <summary>
    /// A founding member who is also paying must be shown the subscription, or they have no way to
    /// see that they are being charged and no way to stop it.
    /// </summary>
    [Fact]
    public void A_subscription_is_reported_ahead_of_Plus_for_life()
    {
        var plus = Subscribed(Now.AddDays(100));

        var byCutOff = PlusAccessRule.Evaluate(Account(CutOff.AddDays(-30), plus), Live(), Now);
        var byGrant = PlusAccessRule.Evaluate(Account(Recently, plus, plusForLife: Now.AddDays(-5)), Live(), Now);

        Assert.Equal(PlusAccessKind.Subscription, byCutOff.Kind);
        Assert.Equal(PlusAccessKind.Subscription, byGrant.Kind);
    }

    [Fact]
    public void A_subscription_is_reported_ahead_of_the_free_days()
    {
        var access = PlusAccessRule.Evaluate(Account(Recently, Subscribed(Now.AddDays(20))), Live(), Now);

        Assert.Equal(PlusAccessKind.Subscription, access.Kind);
        Assert.Null(access.TrialEndsAt);
    }

    /// <summary>Cancelling stops the next payment, not the time already bought.</summary>
    [Fact]
    public void A_cancelled_subscription_still_counts_until_its_period_ends()
    {
        var plus = Subscribed(Now.AddDays(3), Canceled, autoRenewing: false);

        var access = PlusAccessRule.Evaluate(Account(Now.AddDays(-200), plus), Live(), Now);

        Assert.Equal(PlusAccessKind.Subscription, access.Kind);
    }

    /// <summary>
    /// A subscription that has run out is no longer a reason for anything. What the account has is
    /// whatever it would have had without one.
    /// </summary>
    [Fact]
    public void A_subscription_that_has_run_out_falls_through_to_whatever_else_is_true()
    {
        var lapsed = Subscribed(Now.AddDays(-20), Expired, autoRenewing: false);

        var founder = PlusAccessRule.Evaluate(Account(CutOff.AddDays(-30), lapsed), Live(), Now);
        var newcomerStillOnFreeDays = PlusAccessRule.Evaluate(Account(Now.AddDays(-10), lapsed), Live(), Now);
        var newcomerPastThem = PlusAccessRule.Evaluate(Account(Now.AddDays(-60), lapsed), Live(), Now);

        Assert.Equal(PlusAccessKind.Lifetime, founder.Kind);
        Assert.Equal(PlusAccessKind.Trial, newcomerStillOnFreeDays.Kind);
        Assert.Equal(PlusAccessKind.None, newcomerPastThem.Kind);
        Assert.Null(newcomerPastThem.Subscription);
    }

    // ---- Plus for life ----------------------------------------------------------------------

    [Fact]
    public void An_account_from_before_the_cut_off_has_Plus_for_life()
    {
        var access = PlusAccessRule.Evaluate(Account(CutOff.AddSeconds(-1)), Live(), Now);

        Assert.Equal(PlusAccessKind.Lifetime, access.Kind);
        Assert.Null(access.TrialEndsAt);
        Assert.Null(access.Subscription);
        Assert.True(access.HasPlus);
    }

    /// <summary>
    /// The grant written on boot and the cut-off say the same thing about the same accounts. The
    /// date is checked as well so that an account the grant has not reached — it failed, or has not
    /// run yet — is not treated as a newcomer in the meantime.
    /// </summary>
    [Fact]
    public void An_account_from_before_the_cut_off_does_not_need_the_grant_to_have_reached_it()
    {
        var access = PlusAccessRule.Evaluate(Account(CutOff.AddYears(-1), plusForLife: null), Live(), Now);

        Assert.Equal(PlusAccessKind.Lifetime, access.Kind);
    }

    /// <summary>"Before" means before. The promise was to the accounts that existed, not to the first to sign up after.</summary>
    [Fact]
    public void An_account_made_at_the_cut_off_or_after_is_not_from_before_it()
    {
        Assert.NotEqual(PlusAccessKind.Lifetime, PlusAccessRule.Evaluate(Account(CutOff), Live(), Now).Kind);
        Assert.NotEqual(PlusAccessKind.Lifetime, PlusAccessRule.Evaluate(Account(CutOff.AddSeconds(1)), Live(), Now).Kind);
    }

    /// <summary>The field can be set by hand on any account, and is all that one needs.</summary>
    [Fact]
    public void An_account_granted_Plus_for_life_has_it_however_new_it_is()
    {
        var access = PlusAccessRule.Evaluate(Account(Now.AddDays(-400), plusForLife: Now.AddDays(-1)), Live(noCutOff: true), Now);
        var brandNew = PlusAccessRule.Evaluate(Account(Now.AddMinutes(-1), plusForLife: Now), Live(), Now);

        Assert.Equal(PlusAccessKind.Lifetime, access.Kind);
        Assert.Equal(PlusAccessKind.Lifetime, brandNew.Kind);
    }

    /// <summary>A grant whose date nobody wrote down is still a grant.</summary>
    [Fact]
    public void A_grant_with_no_usable_date_is_still_a_grant()
    {
        var access = PlusAccessRule.Evaluate(Account(Recently, plusForLife: GrantDateSerializer.UnknownDate), Live(), Now);

        Assert.Equal(PlusAccessKind.Lifetime, access.Kind);
    }

    [Fact]
    public void With_the_cut_off_switched_off_an_old_account_is_judged_like_any_other()
    {
        var old = Account(CutOff.AddYears(-2));

        var access = PlusAccessRule.Evaluate(old, Live(noCutOff: true), Now);

        // Older than Plus, so its free days ran from the launch, and by now they are over.
        Assert.Equal(PlusAccessKind.None, access.Kind);
        Assert.Equal(LiveSince.AddDays(30), access.TrialEndsAt);
    }

    // ---- The free days ----------------------------------------------------------------------

    [Fact]
    public void A_new_account_has_its_free_days_from_the_day_it_was_made()
    {
        var created = Now.AddDays(-10);

        var access = PlusAccessRule.Evaluate(Account(created), Live(), Now);

        Assert.Equal(PlusAccessKind.Trial, access.Kind);
        Assert.Equal(created.AddDays(30), access.TrialEndsAt);
        Assert.Null(access.Subscription);
        Assert.True(access.HasPlus);
    }

    /// <summary>
    /// The promise that nobody's free days are used up before there was anything to use them on. An
    /// account made after the cut-off but before Plus went live counts its days from the launch.
    /// </summary>
    [Fact]
    public void An_account_older_than_the_launch_has_its_free_days_from_the_launch()
    {
        var created = CutOff.AddDays(6);
        var tenDaysIn = LiveSince.AddDays(10);

        var access = PlusAccessRule.Evaluate(Account(created), Live(), tenDaysIn);

        // Counted from its own creation it would have run out weeks before Plus existed.
        Assert.True(created.AddDays(30) < LiveSince);
        Assert.Equal(PlusAccessKind.Trial, access.Kind);
        Assert.Equal(LiveSince.AddDays(30), access.TrialEndsAt);
    }

    [Fact]
    public void The_free_days_start_from_whichever_is_later_of_the_account_and_the_launch()
    {
        var before = LiveSince.AddDays(-100);
        var after = LiveSince.AddDays(100);

        Assert.Equal(LiveSince.AddDays(30), PlusAccessRule.TrialEndsAt(before, LiveSince, 30));
        Assert.Equal(after.AddDays(30), PlusAccessRule.TrialEndsAt(after, LiveSince, 30));
        Assert.Equal(LiveSince.AddDays(30), PlusAccessRule.TrialEndsAt(LiveSince, LiveSince, 30));
    }

    [Fact]
    public void The_free_days_last_as_long_as_they_are_set_to()
    {
        var created = Now.AddDays(-10);

        Assert.Equal(PlusAccessKind.Trial, PlusAccessRule.Evaluate(Account(created), Live(trialDays: 14), Now).Kind);
        Assert.Equal(PlusAccessKind.None, PlusAccessRule.Evaluate(Account(created), Live(trialDays: 7), Now).Kind);
        Assert.Equal(created.AddDays(14), PlusAccessRule.Evaluate(Account(created), Live(trialDays: 14), Now).TrialEndsAt);
    }

    // ---- Nothing ----------------------------------------------------------------------------

    [Fact]
    public void Once_the_free_days_are_over_there_is_no_access_and_the_app_is_told_when_they_ended()
    {
        var created = Now.AddDays(-45);

        var access = PlusAccessRule.Evaluate(Account(created), Live(), Now);

        Assert.Equal(PlusAccessKind.None, access.Kind);
        Assert.Equal(created.AddDays(30), access.TrialEndsAt);
        Assert.Null(access.Subscription);
        Assert.False(access.HasPlus);
    }

    [Fact]
    public void The_free_days_end_exactly_when_they_say_they_do()
    {
        var created = Now.AddDays(-30);

        Assert.Equal(PlusAccessKind.Trial, PlusAccessRule.Evaluate(Account(created), Live(), Now.AddTicks(-1)).Kind);
        Assert.Equal(PlusAccessKind.None, PlusAccessRule.Evaluate(Account(created), Live(), Now).Kind);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void With_no_free_days_a_new_account_starts_with_none(int trialDays)
    {
        var created = Now.AddMinutes(-1);

        var access = PlusAccessRule.Evaluate(Account(created), Live(trialDays: trialDays), Now);

        Assert.Equal(PlusAccessKind.None, access.Kind);
        Assert.Equal(created, access.TrialEndsAt);
    }

    // ---- How the app is told ----------------------------------------------------------------

    [Theory]
    [InlineData(PlusAccessKind.Open, "open")]
    [InlineData(PlusAccessKind.Subscription, "subscription")]
    [InlineData(PlusAccessKind.Lifetime, "lifetime")]
    [InlineData(PlusAccessKind.Trial, "trial")]
    [InlineData(PlusAccessKind.None, "none")]
    public void Each_kind_of_access_has_the_name_the_app_expects(PlusAccessKind kind, string expected)
    {
        Assert.Equal(expected, PlusAccessRule.NameOf(kind));
    }

    [Fact]
    public void Every_kind_of_access_has_a_name()
    {
        foreach (var kind in Enum.GetValues<PlusAccessKind>())
        {
            Assert.False(string.IsNullOrEmpty(PlusAccessRule.NameOf(kind)));
        }
    }
}
