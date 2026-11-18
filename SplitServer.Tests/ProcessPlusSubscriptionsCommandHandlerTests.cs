using System.Globalization;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using SplitServer.Commands;
using SplitServer.Configuration;
using SplitServer.Models;
using SplitServer.Responses;
using SplitServer.Services;
using SplitServer.Services.Donations;
using SplitServer.Services.Plus;

namespace SplitServer.Tests;

/// <summary>
/// The hourly pass that keeps Buqs Plus true without waiting to be told. Play's notifications are
/// not connected, so for now this pass is the only thing that notices a renewal, a lapse or a
/// refund — and the only thing that tells an annual subscriber a charge is coming. Both jobs fail
/// silently when they are wrong: nobody gets an error, somebody just loses what they paid for, or
/// is charged for a year without warning.
/// </summary>
public class ProcessPlusSubscriptionsCommandHandlerTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private const string Active = "SUBSCRIPTION_STATE_ACTIVE";
    private const string Canceled = "SUBSCRIPTION_STATE_CANCELED";
    private const string Expired = "SUBSCRIPTION_STATE_EXPIRED";

    private readonly FakePlay _play = new();
    private readonly FakePlusSubscriptionsRepository _subscriptions = new();
    private readonly FakePlusUsersRepository _users = new("user-1", "user-2");
    private readonly FakePlusNotificationsRepository _notifications = new();
    private readonly FakePlusUserPreferencesRepository _preferences = new();

    private ProcessPlusSubscriptionsCommandHandler CreateHandler(int reminderDays = 7)
    {
        var catalog = new PlusCatalog(
            Options.Create(
                new PlusSettings
                {
                    Enabled = true,
                    RenewalReminderDays = reminderDays,
                    // The notices about free days ending have tests of their own. Switched off
                    // here, so that everything below is about subscriptions and nothing else.
                    TrialReminderDays = 0,
                    TrialFinalReminderDays = 0,
                }));

        // No transport is configured, so push is a logged no-op and the feed is all that is written.
        var push = new PushNotificationService(
            Options.Create(new PushNotificationsSettings()),
            new UnusedPlusPushSubscriptionsRepository(),
            _preferences);

        return new ProcessPlusSubscriptionsCommandHandler(
            _play,
            catalog,
            new PlusRecorder(_play, catalog, _subscriptions, _users),
            new PlusAccessService(_play, catalog, new FakePlusStateRepository(Now.AddDays(-400))),
            _subscriptions,
            _users,
            _preferences,
            new NotificationService(_notifications, push),
            new LockService());
    }

    private static Task<Result<ProcessPlusSubscriptionsResponse>> Run(ProcessPlusSubscriptionsCommandHandler handler) =>
        handler.Handle(new ProcessPlusSubscriptionsCommand(), CancellationToken.None);

    /// <summary>
    /// Puts a subscription on file as an earlier pass would have left it, with the mark on its
    /// owner that goes with it.
    /// </summary>
    private PlusSubscription OnFile(
        string id = "token-1",
        string userId = "user-1",
        string state = Active,
        DateTime? expiresAt = null,
        bool autoRenewing = true,
        PlusPlan plan = PlusPlan.Annual,
        DateTime? updated = null)
    {
        var row = new PlusSubscription
        {
            Id = id,
            Created = Now.AddDays(-300),
            Updated = updated ?? Now.AddHours(-1),
            UserId = userId,
            ProductId = "buqs_plus",
            BasePlanId = plan == PlusPlan.Annual ? "annual" : "monthly",
            Plan = plan,
            State = state,
            ExpiresAt = expiresAt ?? Now.AddDays(60),
            AutoRenewing = autoRenewing,
            RenewalPrice = 19.99m,
            RenewalCurrency = "GBP",
            LatestOrderId = "GPA.1111-2222",
            StartedAt = Now.AddDays(-300),
            RenewalReminderSentFor = null,
        };

        _subscriptions.Rows[id] = row;

        if (_users.Plus.ContainsKey(userId))
        {
            // Copied without consulting the clock, the way a mark written some time ago sits there
            // until something comes back to it.
            _users.Plus[userId] = PlusEntitlement.ToUserPlus(row);
        }

        return row;
    }

    private static PlaySubscriptionPurchase PlaySays(
        PlusSubscription about,
        string state = Active,
        DateTime? expiresAt = null,
        bool? autoRenewing = null) =>
        new()
        {
            ProductId = about.ProductId,
            BasePlanId = about.BasePlanId,
            State = state,
            IsActive = state == Active,
            HasEnded = state is Canceled or Expired,
            NeedsAcknowledgement = false,
            UserId = about.UserId,
            StartedAt = about.StartedAt,
            ExpiresAt = expiresAt ?? about.ExpiresAt,
            AutoRenewing = autoRenewing ?? state == Active,
            RenewalPrice = about.RenewalPrice,
            RenewalCurrency = about.RenewalCurrency,
            LatestOrderId = about.LatestOrderId,
            LinkedPurchaseToken = null,
        };

    // ---- Noticing renewals and lapses -------------------------------------------------------

    [Fact]
    public async Task Does_nothing_at_all_when_Play_cannot_be_asked()
    {
        _play.IsConfigured = false;
        var row = OnFile(expiresAt: Now.AddHours(-1));

        var result = await Run(CreateHandler());

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.Refreshed);
        Assert.Equal(0, result.Value.Failed);
        Assert.Equal(0, result.Value.Reminded);
        Assert.Equal(0, result.Value.TrialReminded);
        Assert.Empty(_play.Lookups);
        Assert.Equal(row, _subscriptions.Rows["token-1"]);
        Assert.NotNull(_users.Plus["user-1"]);
    }

    /// <summary>The case this pass exists for: a renewal nobody told the server about.</summary>
    [Fact]
    public async Task A_subscription_whose_period_ran_out_is_found_to_have_renewed()
    {
        var row = OnFile(expiresAt: Now.AddMinutes(-10));
        _play.Says("token-1", PlaySays(row, expiresAt: Now.AddDays(365)));

        var result = await Run(CreateHandler());

        Assert.Equal(1, result.Value.Refreshed);
        Assert.Equal(0, result.Value.Failed);
        Assert.Equal(Now.AddDays(365), _subscriptions.Rows["token-1"].ExpiresAt);
        Assert.Equal(Now.AddDays(365), _users.Plus["user-1"]?.ExpiresAt);
    }

    [Fact]
    public async Task A_subscription_whose_period_ran_out_is_found_to_have_lapsed()
    {
        var row = OnFile(expiresAt: Now.AddMinutes(-10));
        _play.Says("token-1", PlaySays(row, state: Expired));

        var result = await Run(CreateHandler());

        Assert.Equal(1, result.Value.Refreshed);
        Assert.Equal(Expired, _subscriptions.Rows["token-1"].State);
        Assert.Null(_users.Plus["user-1"]);
    }

    [Fact]
    public async Task A_subscription_inside_its_period_that_was_read_recently_is_left_alone()
    {
        OnFile(expiresAt: Now.AddDays(60), updated: Now.AddHours(-3));

        var result = await Run(CreateHandler());

        Assert.Equal(0, result.Value.Refreshed);
        Assert.Empty(_play.Lookups);
    }

    /// <summary>
    /// A refund changes nothing the row could have predicted, so every live subscription is looked
    /// at once a day whatever its dates say. Play reports a refunded subscription as expired.
    /// </summary>
    [Fact]
    public async Task A_refund_is_noticed_within_a_day_without_anyone_being_told()
    {
        var row = OnFile(expiresAt: Now.AddDays(200), updated: Now.AddHours(-25));
        _play.Says("token-1", PlaySays(row, state: Expired, expiresAt: Now.AddHours(-2)));

        var result = await Run(CreateHandler());

        Assert.Equal(1, result.Value.Refreshed);
        Assert.Null(_users.Plus["user-1"]);
    }

    [Fact]
    public async Task Finished_subscriptions_are_never_asked_about_again()
    {
        OnFile(id: "lapsed", state: Expired, expiresAt: Now.AddDays(-90), updated: Now.AddDays(-90));
        OnFile(id: "replaced", state: PlusEntitlement.Replaced, expiresAt: Now.AddDays(20), updated: Now.AddDays(-40));

        var result = await Run(CreateHandler());

        Assert.Equal(0, result.Value.Refreshed);
        Assert.Empty(_play.Lookups);
    }

    /// <summary>
    /// "Could not find out" must not be written down as anything. The row keeps what it said, and
    /// a subscriber who is only an hour past their renewal date keeps Plus while Google is down.
    /// </summary>
    [Fact]
    public async Task When_Google_gives_no_answer_the_row_is_left_exactly_as_it_was()
    {
        var row = OnFile(expiresAt: Now.AddMinutes(-10));

        var result = await Run(CreateHandler());

        Assert.Equal(0, result.Value.Refreshed);
        Assert.Equal(1, result.Value.Failed);
        Assert.Equal(row, _subscriptions.Rows["token-1"]);
        Assert.NotNull(_users.Plus["user-1"]);
    }

    /// <summary>
    /// The allowance for an unconfirmed renewal is a few hours, not for ever. Past it, the period
    /// has visibly run out and stops counting whether or not anyone could confirm why.
    /// </summary>
    [Fact]
    public async Task When_Google_gives_no_answer_a_period_long_over_still_stops_counting()
    {
        OnFile(expiresAt: Now.AddHours(-5));
        Assert.NotNull(_users.Plus["user-1"]);

        var result = await Run(CreateHandler());

        Assert.Equal(1, result.Value.Failed);
        Assert.Null(_users.Plus["user-1"]);
    }

    [Fact]
    public async Task One_subscription_Google_will_not_answer_for_does_not_cost_the_rest_their_renewal()
    {
        OnFile(id: "unanswered", userId: "user-1", expiresAt: Now.AddMinutes(-10));
        var answered = OnFile(id: "answered", userId: "user-2", expiresAt: Now.AddMinutes(-10));
        _play.Says("answered", PlaySays(answered, expiresAt: Now.AddDays(30)));

        var result = await Run(CreateHandler());

        Assert.Equal(1, result.Value.Refreshed);
        Assert.Equal(1, result.Value.Failed);
        Assert.Equal(Now.AddDays(30), _users.Plus["user-2"]?.ExpiresAt);
    }

    // ---- Saying a renewal is coming ---------------------------------------------------------

    [Fact]
    public async Task An_annual_subscriber_is_told_before_their_plan_renews()
    {
        var row = OnFile(expiresAt: Now.AddDays(5));
        _play.Says("token-1", PlaySays(row));

        var result = await Run(CreateHandler());

        Assert.Equal(1, result.Value.Reminded);

        var notification = Assert.Single(_notifications.Inserted);
        Assert.Equal("user-1", notification.UserId);
        Assert.Equal("Your Buqs Plus plan renews soon", notification.Title);
        Assert.Equal("/?plus=1", notification.Url);
        Assert.Contains("Your annual plan renews on ", notification.Body);
        Assert.Contains(" for £19.99.", notification.Body);
        Assert.EndsWith("You can manage or cancel it in Google Play.", notification.Body);

        Assert.Equal(row.ExpiresAt, _subscriptions.Rows["token-1"].RenewalReminderSentFor);
    }

    [Fact]
    public async Task A_renewal_is_only_announced_once_however_many_passes_see_it()
    {
        var row = OnFile(expiresAt: Now.AddDays(5));
        _play.Says("token-1", PlaySays(row));
        var handler = CreateHandler();

        await Run(handler);
        var second = await Run(handler);
        var third = await Run(CreateHandler());

        Assert.Equal(0, second.Value.Reminded);
        Assert.Equal(0, third.Value.Reminded);
        Assert.Single(_notifications.Inserted);
    }

    /// <summary>
    /// The row can be most of a day old. Someone who cancelled this morning must not be told this
    /// afternoon that their plan is about to renew, so Google is asked again first.
    /// </summary>
    [Fact]
    public async Task Someone_who_has_cancelled_since_the_row_was_read_is_not_told_their_plan_will_renew()
    {
        var row = OnFile(expiresAt: Now.AddDays(5));
        _play.Says("token-1", PlaySays(row, state: Canceled, autoRenewing: false));

        var result = await Run(CreateHandler());

        Assert.Equal(0, result.Value.Reminded);
        Assert.Empty(_notifications.Inserted);
        Assert.Null(_subscriptions.Rows["token-1"].RenewalReminderSentFor);
        // They keep what they paid for; they are just not being charged again.
        Assert.False(_users.Plus["user-1"]?.AutoRenewing);
    }

    /// <summary>
    /// A reminder that is right as far as anyone knows beats a renewal nobody was warned about.
    /// </summary>
    [Fact]
    public async Task The_reminder_still_goes_out_when_Google_cannot_be_asked_first()
    {
        OnFile(expiresAt: Now.AddDays(5));

        var result = await Run(CreateHandler());

        Assert.Equal(1, result.Value.Reminded);
        Assert.Single(_notifications.Inserted);
    }

    [Fact]
    public async Task A_renewal_that_is_still_far_off_is_not_announced()
    {
        var row = OnFile(expiresAt: Now.AddDays(40));
        _play.Says("token-1", PlaySays(row));

        var result = await Run(CreateHandler());

        Assert.Equal(0, result.Value.Reminded);
        Assert.Empty(_notifications.Inserted);
    }

    /// <summary>A notice every four weeks is noise, not a warning.</summary>
    [Fact]
    public async Task A_monthly_subscriber_is_not_reminded()
    {
        var row = OnFile(plan: PlusPlan.Monthly, expiresAt: Now.AddDays(5));
        _play.Says("token-1", PlaySays(row));

        var result = await Run(CreateHandler());

        Assert.Equal(0, result.Value.Reminded);
        Assert.Empty(_notifications.Inserted);
    }

    [Fact]
    public async Task Reminders_can_be_switched_off()
    {
        var row = OnFile(expiresAt: Now.AddDays(5));
        _play.Says("token-1", PlaySays(row));

        var result = await Run(CreateHandler(reminderDays: 0));

        Assert.Equal(0, result.Value.Reminded);
        Assert.Empty(_notifications.Inserted);
    }

    /// <summary>
    /// A renewal late in the evening UTC is on the next day in Tokyo, and the date in the notice
    /// should be the one the subscriber will actually see the charge on.
    /// </summary>
    [Fact]
    public async Task The_reminder_gives_the_date_in_the_subscribers_own_time_zone()
    {
        var renewsAt = Now.Date.AddDays(5).AddHours(23).AddMinutes(30);
        var row = OnFile(expiresAt: renewsAt);
        _play.Says("token-1", PlaySays(row));
        _preferences.TimeZones["user-1"] = "Asia/Tokyo";

        await Run(CreateHandler());

        var notification = Assert.Single(_notifications.Inserted);
        var utcDate = renewsAt.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
        var tokyoDate = renewsAt.AddDays(1).ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

        Assert.Contains($"renews on {tokyoDate} for", notification.Body);
        Assert.DoesNotContain($"renews on {utcDate} for", notification.Body);
    }
}
