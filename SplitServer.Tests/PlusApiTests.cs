using System.Text.Json;
using Microsoft.Extensions.Options;
using SplitServer.Commands;
using SplitServer.Configuration;
using SplitServer.Models;
using SplitServer.Queries;
using SplitServer.Responses;
using SplitServer.Services.Donations;
using SplitServer.Services.Plus;

namespace SplitServer.Tests;

/// <summary>
/// The Plus endpoints as the app sees them: what is on offer, what registering a purchase does, and
/// the exact shape of what comes back. The app is built against these field names and values, so
/// they are pinned as literal JSON — a rename here is a silent break over there, with nothing in
/// either codebase failing to compile.
/// </summary>
public class PlusApiTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    /// <summary>The same options ASP.NET's minimal APIs serialise responses with.</summary>
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private readonly FakePlay _play = new();
    private readonly FakePlusSubscriptionsRepository _subscriptions = new();
    private readonly FakePlusUsersRepository _users = new("user-1");

    private static PlusCatalog Catalog(bool enabled) =>
        new(Options.Create(new PlusSettings { Enabled = enabled }));

    private async Task<GetPlusOfferResponse> GetOffer(bool enabled)
    {
        var result = await new GetPlusOfferQueryHandler(_play, Catalog(enabled))
            .Handle(new GetPlusOfferQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);

        return result.Value;
    }

    private RegisterPlusPurchaseCommandHandler RegisterHandler(bool enabled = true)
    {
        var catalog = Catalog(enabled);

        return new RegisterPlusPurchaseCommandHandler(
            _users,
            _play,
            new PlusRecorder(_play, catalog, _subscriptions, _users));
    }

    private static PlaySubscriptionPurchase ActiveAnnual(string userId = "user-1") =>
        new()
        {
            ProductId = "buqs_plus",
            BasePlanId = "annual",
            State = "SUBSCRIPTION_STATE_ACTIVE",
            IsActive = true,
            HasEnded = false,
            NeedsAcknowledgement = true,
            UserId = userId,
            StartedAt = Now,
            ExpiresAt = Now.AddDays(365),
            AutoRenewing = true,
            RenewalPrice = 19.99m,
            RenewalCurrency = "GBP",
            LatestOrderId = "GPA.1111-2222",
            LinkedPurchaseToken = null,
        };

    // ---- GET /plus/offer --------------------------------------------------------------------

    [Fact]
    public async Task Offers_both_plans_annual_first_when_Plus_is_on_sale()
    {
        var offer = await GetOffer(enabled: true);

        Assert.Equal(
            """{"isAvailable":true,"productId":"buqs_plus","plans":[{"plan":"annual","basePlanId":"annual"},{"plan":"monthly","basePlanId":"monthly"}]}""",
            JsonSerializer.Serialize(offer, Web));
    }

    [Fact]
    public async Task Offers_nothing_while_Plus_is_switched_off()
    {
        var offer = await GetOffer(enabled: false);

        Assert.Equal(
            """{"isAvailable":false,"productId":"buqs_plus","plans":[]}""",
            JsonSerializer.Serialize(offer, Web));
    }

    /// <summary>
    /// Switched on with no way to verify a purchase would sell something this server could never
    /// grant.
    /// </summary>
    [Fact]
    public async Task Offers_nothing_when_Play_cannot_be_asked_even_if_Plus_is_switched_on()
    {
        _play.IsConfigured = false;

        var offer = await GetOffer(enabled: true);

        Assert.False(offer.IsAvailable);
        Assert.Empty(offer.Plans);
    }

    // ---- POST /plus/purchase ----------------------------------------------------------------

    [Fact]
    public async Task Registering_a_real_purchase_gives_the_caller_Plus()
    {
        _play.Says("token-1", ActiveAnnual());

        var result = await RegisterHandler().Handle(
            new RegisterPlusPurchaseCommand { UserId = "user-1", PurchaseToken = "token-1" },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(_users.Plus["user-1"]);
    }

    /// <summary>
    /// The switch decides whether a price is shown. By the time a token arrives Play has already
    /// taken the money, and refusing to record it would charge someone for nothing.
    /// </summary>
    [Fact]
    public async Task A_purchase_is_still_recorded_while_Plus_is_switched_off_for_sale()
    {
        _play.Says("token-1", ActiveAnnual());

        var result = await RegisterHandler(enabled: false).Handle(
            new RegisterPlusPurchaseCommand { UserId = "user-1", PurchaseToken = "token-1" },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(_users.Plus["user-1"]);
    }

    [Fact]
    public async Task Registering_is_refused_when_Play_cannot_be_asked()
    {
        _play.IsConfigured = false;
        _play.Says("token-1", ActiveAnnual());

        var result = await RegisterHandler().Handle(
            new RegisterPlusPurchaseCommand { UserId = "user-1", PurchaseToken = "token-1" },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Buqs Plus is not available", result.Error);
        Assert.Empty(_play.Lookups);
        Assert.Null(_users.Plus["user-1"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Registering_without_a_token_is_refused_before_Google_is_asked(string purchaseToken)
    {
        var result = await RegisterHandler().Handle(
            new RegisterPlusPurchaseCommand { UserId = "user-1", PurchaseToken = purchaseToken },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_play.Lookups);
    }

    [Fact]
    public async Task Registering_for_an_account_that_does_not_exist_is_refused()
    {
        _play.Says("token-1", ActiveAnnual(userId: "nobody"));

        var result = await RegisterHandler().Handle(
            new RegisterPlusPurchaseCommand { UserId = "nobody", PurchaseToken = "token-1" },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_subscriptions.Rows);
    }

    [Fact]
    public async Task Registering_a_token_Google_does_not_recognise_gives_nothing()
    {
        var result = await RegisterHandler().Handle(
            new RegisterPlusPurchaseCommand { UserId = "user-1", PurchaseToken = "made-up" },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Null(_users.Plus["user-1"]);
    }

    // ---- "plus" on GET /users/me ------------------------------------------------------------

    private static string Json(PlusAccess access) =>
        JsonSerializer.Serialize(PlusAccessResponse.From(access, Now), Web);

    private static readonly UserPlus AnnualMark = new()
    {
        Plan = PlusPlan.Annual,
        ExpiresAt = new DateTime(2099, 12, 12, 9, 30, 0, DateTimeKind.Utc),
        AutoRenewing = true,
        State = "SUBSCRIPTION_STATE_ACTIVE",
        SubscriptionId = "token-1",
        RenewalPrice = 19.99m,
        RenewalCurrency = "GBP",
        Since = new DateTime(2026, 12, 12, 9, 30, 0, DateTimeKind.Utc),
    };

    /// <summary>Plus is not live: nothing is sold and nothing is locked.</summary>
    [Fact]
    public void An_account_is_described_as_open_while_Plus_is_not_live()
    {
        var access = new PlusAccess { Kind = PlusAccessKind.Open, TrialEndsAt = null, Subscription = null };

        Assert.Equal("""{"access":"open","trialEndsAt":null,"subscription":null}""", Json(access));
    }

    [Fact]
    public void A_subscriber_is_described_with_their_subscription()
    {
        var access = new PlusAccess { Kind = PlusAccessKind.Subscription, TrialEndsAt = null, Subscription = AnnualMark };

        Assert.Equal(
            """{"access":"subscription","trialEndsAt":null,"subscription":{"plan":"annual","expiresAt":"2099-12-12T09:30:00Z","autoRenewing":true,"inGracePeriod":false,"renewalPrice":19.99,"renewalCurrencyCode":"GBP"}}""",
            Json(access));
    }

    [Fact]
    public void A_subscriber_whose_renewal_price_Play_did_not_give_has_nulls_there()
    {
        var mark = AnnualMark with
        {
            Plan = PlusPlan.Monthly,
            ExpiresAt = new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            AutoRenewing = false,
            State = "SUBSCRIPTION_STATE_CANCELED",
            RenewalPrice = null,
            RenewalCurrency = null,
        };

        var access = new PlusAccess { Kind = PlusAccessKind.Subscription, TrialEndsAt = null, Subscription = mark };

        Assert.Equal(
            """{"access":"subscription","trialEndsAt":null,"subscription":{"plan":"monthly","expiresAt":"2099-01-01T00:00:00Z","autoRenewing":false,"inGracePeriod":false,"renewalPrice":null,"renewalCurrencyCode":null}}""",
            Json(access));
    }

    [Fact]
    public void A_subscriber_whose_payment_is_failing_is_described_as_in_the_grace_period()
    {
        var access = new PlusAccess
        {
            Kind = PlusAccessKind.Subscription,
            TrialEndsAt = null,
            Subscription = AnnualMark with { State = "SUBSCRIPTION_STATE_IN_GRACE_PERIOD" },
        };

        Assert.True(PlusAccessResponse.From(access, Now).Subscription?.InGracePeriod);
        Assert.Contains("inGracePeriod", Json(access));
    }

    [Fact]
    public void An_account_with_Plus_for_life_is_described_as_lifetime()
    {
        var access = new PlusAccess { Kind = PlusAccessKind.Lifetime, TrialEndsAt = null, Subscription = null };

        Assert.Equal("""{"access":"lifetime","trialEndsAt":null,"subscription":null}""", Json(access));
    }

    [Fact]
    public void An_account_on_its_free_days_is_told_when_they_end()
    {
        var access = new PlusAccess
        {
            Kind = PlusAccessKind.Trial,
            TrialEndsAt = new DateTime(2027, 1, 31, 9, 0, 0, DateTimeKind.Utc),
            Subscription = null,
        };

        Assert.Equal("""{"access":"trial","trialEndsAt":"2027-01-31T09:00:00Z","subscription":null}""", Json(access));
    }

    [Fact]
    public void An_account_past_its_free_days_is_told_when_they_ended()
    {
        var access = new PlusAccess
        {
            Kind = PlusAccessKind.None,
            TrialEndsAt = new DateTime(2027, 1, 31, 9, 0, 0, DateTimeKind.Utc),
            Subscription = null,
        };

        Assert.Equal("""{"access":"none","trialEndsAt":"2027-01-31T09:00:00Z","subscription":null}""", Json(access));
    }

    /// <summary>
    /// The subscription is only ever described for someone whose access is that subscription. An
    /// account that has Plus some other way gets none, whatever mark is lying about on it.
    /// </summary>
    [Theory]
    [InlineData(PlusAccessKind.Open)]
    [InlineData(PlusAccessKind.Lifetime)]
    [InlineData(PlusAccessKind.Trial)]
    [InlineData(PlusAccessKind.None)]
    public void No_subscription_is_described_unless_access_is_by_subscription(PlusAccessKind kind)
    {
        var access = new PlusAccess { Kind = kind, TrialEndsAt = null, Subscription = AnnualMark };

        Assert.Null(PlusAccessResponse.From(access, Now).Subscription);
    }

    /// <summary>The whole path the endpoint takes: a real account, the service, the rule and the response.</summary>
    [Fact]
    public async Task A_new_account_on_a_server_where_Plus_is_live_comes_back_as_on_its_free_days()
    {
        var liveSince = Now.AddDays(-100);
        var created = Now.AddDays(-3);
        _users.Add("newcomer", created);

        var service = new PlusAccessService(
            _play,
            new PlusCatalog(Options.Create(new PlusSettings { Enabled = true, LifetimeBefore = Now.AddDays(-200) })),
            new FakePlusStateRepository(liveSince));

        var response = PlusAccessResponse.From(await service.For(_users.Get("newcomer"), Now, CancellationToken.None), Now);

        Assert.Equal("trial", response.Access);
        Assert.Equal(created.AddDays(30), response.TrialEndsAt);
        Assert.Null(response.Subscription);
    }
}
