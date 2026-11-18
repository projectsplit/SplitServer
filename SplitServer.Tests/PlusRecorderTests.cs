using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using SplitServer.Configuration;
using SplitServer.Models;
using SplitServer.Services.Donations;
using SplitServer.Services.Plus;

namespace SplitServer.Tests;

/// <summary>
/// What happens to a Plus subscription once Google has answered. Google itself is faked: these are
/// not about talking to Play, they are about what this server does with what Play says — who gets
/// Plus, who must not, and that being told the same thing twice changes nothing.
///
/// Every case goes through the one entry point the app, the notification endpoint and the worker
/// all share, because the point of there being only one is that none of them can get a different
/// answer from the others.
/// </summary>
public class PlusRecorderTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private const string Active = "SUBSCRIPTION_STATE_ACTIVE";
    private const string Canceled = "SUBSCRIPTION_STATE_CANCELED";
    private const string Expired = "SUBSCRIPTION_STATE_EXPIRED";
    private const string OnHold = "SUBSCRIPTION_STATE_ON_HOLD";

    private readonly FakePlay _play = new();
    private readonly FakePlusSubscriptionsRepository _subscriptions = new();
    private readonly FakePlusUsersRepository _users = new("user-1", "user-2");

    public PlusRecorderTests()
    {
        _play.RowExists = token => _subscriptions.Rows.ContainsKey(token);
    }

    private PlusRecorder CreateRecorder() =>
        new(_play, new PlusCatalog(Options.Create(new PlusSettings { Enabled = true })), _subscriptions, _users);

    private static PlaySubscriptionPurchase Purchase(
        string? userId = "user-1",
        string? productId = "buqs_plus",
        string? basePlanId = "annual",
        string state = Active,
        DateTime? expiresAt = null,
        bool autoRenewing = true,
        bool needsAcknowledgement = false,
        string? linkedPurchaseToken = null) =>
        new()
        {
            ProductId = productId,
            BasePlanId = basePlanId,
            State = state,
            IsActive = state == Active,
            HasEnded = state is Canceled or Expired,
            NeedsAcknowledgement = needsAcknowledgement,
            UserId = userId,
            StartedAt = Now.AddDays(-1),
            ExpiresAt = expiresAt ?? Now.AddDays(364),
            AutoRenewing = autoRenewing,
            RenewalPrice = 19.99m,
            RenewalCurrency = "gbp",
            LatestOrderId = "GPA.1111-2222",
            LinkedPurchaseToken = linkedPurchaseToken,
        };

    // ---- A purchase becomes Plus ------------------------------------------------------------

    [Fact]
    public async Task A_verified_purchase_gives_its_buyer_Plus()
    {
        _play.Says("token-1", Purchase(needsAcknowledgement: true));

        var result = await CreateRecorder().Record("token-1", "user-1", CancellationToken.None);

        Assert.True(result.IsSuccess);

        var row = _subscriptions.Rows["token-1"];
        Assert.Equal("user-1", row.UserId);
        Assert.Equal("buqs_plus", row.ProductId);
        Assert.Equal("annual", row.BasePlanId);
        Assert.Equal(PlusPlan.Annual, row.Plan);
        Assert.Equal(Active, row.State);
        Assert.True(row.AutoRenewing);
        Assert.Equal(19.99m, row.RenewalPrice);
        Assert.Equal("GBP", row.RenewalCurrency);
        Assert.Equal("GPA.1111-2222", row.LatestOrderId);
        Assert.Null(row.RenewalReminderSentFor);

        var mark = _users.Plus["user-1"];
        Assert.NotNull(mark);
        Assert.Equal(PlusPlan.Annual, mark.Plan);
        Assert.Equal("token-1", mark.SubscriptionId);
        Assert.Equal(row.ExpiresAt, mark.ExpiresAt);
        Assert.True(mark.AutoRenewing);
    }

    [Fact]
    public async Task The_monthly_plan_is_recorded_as_monthly()
    {
        _play.Says("token-1", Purchase(basePlanId: "monthly", expiresAt: Now.AddDays(30)));

        await CreateRecorder().Record("token-1", "user-1", CancellationToken.None);

        Assert.Equal(PlusPlan.Monthly, _subscriptions.Rows["token-1"].Plan);
        Assert.Equal(PlusPlan.Monthly, _users.Plus["user-1"]?.Plan);
    }

    /// <summary>
    /// Play refunds a subscription left unacknowledged for three days. Acknowledging is what makes
    /// the purchase stick, and it comes only after the record of it exists.
    /// </summary>
    [Fact]
    public async Task A_new_purchase_is_acknowledged_once_it_has_been_recorded()
    {
        _play.Says("token-1", Purchase(needsAcknowledgement: true));

        await CreateRecorder().Record("token-1", "user-1", CancellationToken.None);

        Assert.Equal(("buqs_plus", "token-1"), Assert.Single(_play.Acknowledged));
        Assert.True(_play.RowExistedWhenAcknowledged);
    }

    [Fact]
    public async Task A_purchase_Play_already_has_as_acknowledged_is_not_acknowledged_again()
    {
        _play.Says("token-1", Purchase(needsAcknowledgement: false));

        await CreateRecorder().Record("token-1", "user-1", CancellationToken.None);

        Assert.Empty(_play.Acknowledged);
    }

    /// <summary>
    /// The subscription is recorded and the payment is real, so this must surface as an error and
    /// be retried. Swallowing it would let Play refund something already granted.
    /// </summary>
    [Fact]
    public async Task Failing_to_acknowledge_is_an_error_rather_than_a_quiet_success()
    {
        _play.Says("token-1", Purchase(needsAcknowledgement: true));
        _play.AcknowledgeResult = Result.Failure("Play said no");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateRecorder().Record("token-1", "user-1", CancellationToken.None));

        // What was written stays written: the retry finds it and only has the acknowledgement left.
        Assert.True(_subscriptions.Rows.ContainsKey("token-1"));
        Assert.NotNull(_users.Plus["user-1"]);
    }

    /// <summary>
    /// A slow payment method leaves a real purchase whose money has not arrived. It is written down
    /// so that it can be followed, but it gives nothing yet, and Play must not be told to keep money
    /// it has not received: the attempt is refused, and would otherwise fail the whole recording.
    /// </summary>
    [Fact]
    public async Task A_purchase_whose_payment_is_still_pending_is_recorded_but_gives_nothing_yet()
    {
        _play.Says("token-1", Purchase(state: "SUBSCRIPTION_STATE_PENDING", needsAcknowledgement: true));

        var result = await CreateRecorder().Record("token-1", "user-1", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("SUBSCRIPTION_STATE_PENDING", _subscriptions.Rows["token-1"].State);
        Assert.Null(_users.Plus["user-1"]);
        Assert.Empty(_play.Acknowledged);
    }

    [Fact]
    public async Task A_pending_purchase_becomes_Plus_and_is_acknowledged_once_the_money_arrives()
    {
        var recorder = CreateRecorder();

        _play.Says("token-1", Purchase(state: "SUBSCRIPTION_STATE_PENDING", needsAcknowledgement: true));
        await recorder.Record("token-1", "user-1", CancellationToken.None);

        _play.Says("token-1", Purchase(needsAcknowledgement: true));
        await recorder.Record("token-1", expectedUserId: null, CancellationToken.None);

        Assert.NotNull(_users.Plus["user-1"]);
        Assert.Equal(("buqs_plus", "token-1"), Assert.Single(_play.Acknowledged));
    }

    // ---- Whose purchase it is ---------------------------------------------------------------

    /// <summary>
    /// The shape of stealing somebody's subscription by replaying their token. Nothing may be
    /// written for either account.
    /// </summary>
    [Fact]
    public async Task Someone_elses_purchase_cannot_be_claimed()
    {
        _play.Says("token-1", Purchase(userId: "user-1", needsAcknowledgement: true));

        var result = await CreateRecorder().Record("token-1", "user-2", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Purchase belongs to another account", result.Error);
        Assert.Empty(_subscriptions.Rows);
        Assert.Null(_users.Plus["user-1"]);
        Assert.Null(_users.Plus["user-2"]);
        Assert.Empty(_play.Acknowledged);
    }

    /// <summary>A notification from Play carries no caller. Play's own account id is enough.</summary>
    [Fact]
    public async Task A_notification_is_credited_to_the_account_Play_names()
    {
        _play.Says("token-1", Purchase(userId: "user-2"));

        var result = await CreateRecorder().Record("token-1", expectedUserId: null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("user-2", _subscriptions.Rows["token-1"].UserId);
        Assert.NotNull(_users.Plus["user-2"]);
        Assert.Null(_users.Plus["user-1"]);
    }

    /// <summary>
    /// A renewal months later can arrive with nothing on it but the token. Who is paying was written
    /// down at purchase time for exactly this.
    /// </summary>
    [Fact]
    public async Task A_notification_that_names_nobody_falls_back_to_the_row_on_file()
    {
        var recorder = CreateRecorder();

        _play.Says("token-1", Purchase(userId: "user-1"));
        await recorder.Record("token-1", "user-1", CancellationToken.None);

        _play.Says("token-1", Purchase(userId: null, expiresAt: Now.AddDays(729)));
        var result = await recorder.Record("token-1", expectedUserId: null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("user-1", _subscriptions.Rows["token-1"].UserId);
        Assert.Equal(_subscriptions.Rows["token-1"].ExpiresAt, _users.Plus["user-1"]?.ExpiresAt);
    }

    [Fact]
    public async Task A_purchase_nobody_can_be_tied_to_is_refused()
    {
        _play.Says("token-1", Purchase(userId: null));

        var result = await CreateRecorder().Record("token-1", expectedUserId: null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_subscriptions.Rows);
    }

    // ---- What is not Plus -------------------------------------------------------------------

    /// <summary>
    /// Play verifies any subscription bought against the app, a monthly tip included. Only the one
    /// product may ever be read as Plus.
    /// </summary>
    [Theory]
    [InlineData("support_monthly")]
    [InlineData("support_monthly_500")]
    [InlineData(null)]
    public async Task A_subscription_to_something_else_is_not_Plus(string? productId)
    {
        _play.Says("token-1", Purchase(productId: productId, basePlanId: "monthly", needsAcknowledgement: true));

        var result = await CreateRecorder().Record("token-1", "user-1", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_subscriptions.Rows);
        Assert.Null(_users.Plus["user-1"]);
        Assert.Empty(_play.Acknowledged);
    }

    [Theory]
    [InlineData("weekly")]
    [InlineData(null)]
    public async Task A_plan_this_server_does_not_know_is_refused(string? basePlanId)
    {
        _play.Says("token-1", Purchase(basePlanId: basePlanId));

        var result = await CreateRecorder().Record("token-1", "user-1", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_subscriptions.Rows);
        Assert.Null(_users.Plus["user-1"]);
    }

    /// <summary>"Could not find out" must never be written down as anything.</summary>
    [Fact]
    public async Task Nothing_is_written_when_Google_cannot_be_asked()
    {
        var result = await CreateRecorder().Record("unknown-token", "user-1", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_subscriptions.Rows);
        Assert.Null(_users.Plus["user-1"]);
    }

    // ---- The same subscription over time ----------------------------------------------------

    /// <summary>
    /// Play redelivers notifications and the app retries registrations. Both can arrive for the
    /// same purchase, and the second must leave exactly what the first did.
    /// </summary>
    [Fact]
    public async Task Being_told_twice_changes_nothing()
    {
        var recorder = CreateRecorder();
        _play.Says("token-1", Purchase());

        await recorder.Record("token-1", "user-1", CancellationToken.None);
        var first = _subscriptions.Rows["token-1"];
        var firstMark = _users.Plus["user-1"];

        await recorder.Record("token-1", "user-1", CancellationToken.None);
        var second = _subscriptions.Rows["token-1"];

        Assert.Single(_subscriptions.Rows);
        Assert.Equal(first.Created, second.Created);
        Assert.Equal(first with { Updated = second.Updated }, second);
        Assert.Equal(firstMark, _users.Plus["user-1"]);
    }

    [Fact]
    public async Task A_renewal_moves_the_date_on_and_keeps_everything_else()
    {
        var recorder = CreateRecorder();

        _play.Says("token-1", Purchase(expiresAt: Now.AddDays(3)));
        await recorder.Record("token-1", "user-1", CancellationToken.None);

        var created = _subscriptions.Rows["token-1"].Created;
        var announced = Now.AddDays(3);
        Assert.True(await _subscriptions.TryClaimRenewalReminder("token-1", announced, CancellationToken.None));

        _play.Says("token-1", Purchase(expiresAt: Now.AddDays(368)));
        await recorder.Record("token-1", expectedUserId: null, CancellationToken.None);

        var row = _subscriptions.Rows["token-1"];
        Assert.Equal(Now.AddDays(368), row.ExpiresAt);
        Assert.Equal(created, row.Created);
        // Still says which renewal was announced, so the one just gone is not announced again and
        // next year's — a different date — will be.
        Assert.Equal(announced, row.RenewalReminderSentFor);
        Assert.Equal(Now.AddDays(368), _users.Plus["user-1"]?.ExpiresAt);
    }

    /// <summary>Cancelling stops the next payment, not the time already bought.</summary>
    [Fact]
    public async Task Cancelling_keeps_Plus_until_the_period_ends()
    {
        var recorder = CreateRecorder();

        _play.Says("token-1", Purchase());
        await recorder.Record("token-1", "user-1", CancellationToken.None);

        _play.Says("token-1", Purchase(state: Canceled, autoRenewing: false, expiresAt: Now.AddDays(20)));
        await recorder.Record("token-1", expectedUserId: null, CancellationToken.None);

        var mark = _users.Plus["user-1"];
        Assert.NotNull(mark);
        Assert.False(mark.AutoRenewing);
        Assert.Equal(Canceled, mark.State);
        Assert.Equal(Now.AddDays(20), mark.ExpiresAt);
    }

    [Theory]
    // Ran out, or was refunded: Play reports both as expired.
    [InlineData(Expired)]
    // A payment that failed for good. Not paying today, though it may start again by itself.
    [InlineData(OnHold)]
    public async Task Plus_is_taken_away_when_Play_says_it_is_no_longer_paid_for(string state)
    {
        var recorder = CreateRecorder();

        _play.Says("token-1", Purchase());
        await recorder.Record("token-1", "user-1", CancellationToken.None);
        Assert.NotNull(_users.Plus["user-1"]);

        _play.Says("token-1", Purchase(state: state, autoRenewing: state == OnHold, expiresAt: Now.AddMinutes(-10)));
        var result = await recorder.Record("token-1", expectedUserId: null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(state, _subscriptions.Rows["token-1"].State);
        Assert.Null(_users.Plus["user-1"]);
    }

    [Fact]
    public async Task Plus_comes_back_when_a_payment_on_hold_goes_through()
    {
        var recorder = CreateRecorder();

        _play.Says("token-1", Purchase(state: OnHold, expiresAt: Now.AddDays(-2)));
        await recorder.Record("token-1", "user-1", CancellationToken.None);
        Assert.Null(_users.Plus["user-1"]);

        _play.Says("token-1", Purchase(expiresAt: Now.AddDays(30)));
        await recorder.Record("token-1", expectedUserId: null, CancellationToken.None);

        Assert.NotNull(_users.Plus["user-1"]);
    }

    // ---- One subscription replacing another --------------------------------------------------

    /// <summary>
    /// Changing plan or resubscribing mints a new token that points back at the old one. The person
    /// has one subscription, and the record has to end up saying so.
    /// </summary>
    [Fact]
    public async Task A_purchase_that_replaces_another_stands_the_old_one_down()
    {
        var recorder = CreateRecorder();

        _play.Says("old-token", Purchase(basePlanId: "monthly", expiresAt: Now.AddDays(12)));
        await recorder.Record("old-token", "user-1", CancellationToken.None);

        _play.Says("new-token", Purchase(basePlanId: "annual", expiresAt: Now.AddDays(365), linkedPurchaseToken: "old-token"));
        await recorder.Record("new-token", "user-1", CancellationToken.None);

        Assert.Equal(PlusEntitlement.Replaced, _subscriptions.Rows["old-token"].State);
        Assert.False(_subscriptions.Rows["old-token"].AutoRenewing);
        Assert.Equal(Active, _subscriptions.Rows["new-token"].State);

        var mark = _users.Plus["user-1"];
        Assert.NotNull(mark);
        Assert.Equal("new-token", mark.SubscriptionId);
        Assert.Equal(PlusPlan.Annual, mark.Plan);
    }

    /// <summary>
    /// A plan changed in the Play Store rather than in the app can arrive as a bare notification:
    /// no caller, and no account id on the new purchase. The one it replaces says whose it is.
    /// </summary>
    [Fact]
    public async Task A_replacement_that_names_nobody_belongs_to_whoever_had_the_one_it_replaces()
    {
        var recorder = CreateRecorder();

        _play.Says("old-token", Purchase(userId: "user-2", basePlanId: "monthly"));
        await recorder.Record("old-token", "user-2", CancellationToken.None);

        _play.Says("new-token", Purchase(userId: null, linkedPurchaseToken: "old-token"));
        var result = await recorder.Record("new-token", expectedUserId: null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("user-2", _subscriptions.Rows["new-token"].UserId);
        Assert.Equal("new-token", _users.Plus["user-2"]?.SubscriptionId);
    }

    /// <summary>
    /// Play can go on describing a token as inside its paid period after a newer one has taken over.
    /// Writing that back would give one person two live subscriptions on paper.
    /// </summary>
    [Fact]
    public async Task A_replaced_subscription_stays_replaced_whatever_Play_says_about_it_later()
    {
        var recorder = CreateRecorder();

        _play.Says("old-token", Purchase(basePlanId: "monthly", expiresAt: Now.AddDays(12)));
        await recorder.Record("old-token", "user-1", CancellationToken.None);

        _play.Says("new-token", Purchase(linkedPurchaseToken: "old-token"));
        await recorder.Record("new-token", "user-1", CancellationToken.None);

        var result = await recorder.Record("old-token", expectedUserId: null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PlusEntitlement.Replaced, _subscriptions.Rows["old-token"].State);
        Assert.Equal("new-token", _users.Plus["user-1"]?.SubscriptionId);
    }

    /// <summary>
    /// One Google account can pay while signed in to two different Buqs accounts. The account whose
    /// subscription was replaced has just lost it, and nothing else would ever come back to say so.
    /// </summary>
    [Fact]
    public async Task Replacing_a_subscription_credited_to_another_account_takes_Plus_off_that_account()
    {
        var recorder = CreateRecorder();

        _play.Says("old-token", Purchase(userId: "user-1", basePlanId: "monthly", expiresAt: Now.AddDays(12)));
        await recorder.Record("old-token", "user-1", CancellationToken.None);
        Assert.NotNull(_users.Plus["user-1"]);

        _play.Says("new-token", Purchase(userId: "user-2", linkedPurchaseToken: "old-token"));
        await recorder.Record("new-token", "user-2", CancellationToken.None);

        Assert.Null(_users.Plus["user-1"]);
        Assert.Equal("new-token", _users.Plus["user-2"]?.SubscriptionId);
    }

    // ---- An account that is gone -------------------------------------------------------------

    /// <summary>
    /// A deleted account's subscription is still followed to its end, but there is no user left to
    /// mark, and the write must not bring one back.
    /// </summary>
    [Fact]
    public async Task A_subscription_whose_account_was_deleted_is_recorded_without_recreating_the_account()
    {
        _play.Says("token-1", Purchase(userId: "deleted-user", state: Canceled, autoRenewing: false));

        var result = await CreateRecorder().Record("token-1", expectedUserId: null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("deleted-user", _subscriptions.Rows["token-1"].UserId);
        Assert.False(_users.Plus.ContainsKey("deleted-user"));
    }
}
