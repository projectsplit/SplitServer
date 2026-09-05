using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SplitServer.Configuration;
using SplitServer.Models;
using SplitServer.Services.Donations;

namespace SplitServer.Tests;

/// <summary>
/// The cadence rules. Getting these wrong is not a visible bug — nothing crashes, the prompt just
/// quietly starts turning up too often, and by the time anyone notices it has already annoyed
/// everybody. So each gate is pinned individually, and the sequence of gaps is pinned as a whole.
/// </summary>
public class DonationPromptPolicyTests
{
    private static readonly DateTime Now = new(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);

    private static DonationPromptPolicy CreatePolicy(DonationsSettings? settings = null) =>
        new(Options.Create(settings ?? new DonationsSettings()));

    private static DonationPromptState State(
        DateTime? lastPrompted = null,
        int promptCount = 0,
        DateTime? lastDonated = null,
        bool optedOut = false,
        bool hasActiveMonthly = false,
        DateTime? engagementReachedAt = null) =>
        new()
        {
            Id = "user-1",
            Created = Now.AddYears(-1),
            Updated = Now,
            LastPromptedAt = lastPrompted,
            PromptCount = promptCount,
            LastDonatedAt = lastDonated,
            OptedOut = optedOut,
            EngagementReachedAt = engagementReachedAt,
            HasActiveMonthly = hasActiveMonthly,
        };

    /// <summary>An account old enough that age is never the thing being tested.</summary>
    private static readonly DateTime OldAccount = Now.AddYears(-2);

    [Fact]
    public void Asks_a_settled_account_that_has_never_been_asked()
    {
        var block = CreatePolicy().EvaluateWithoutEngagement(State(), OldAccount, Now);

        Assert.Equal(DonationPromptBlock.None, block);
    }

    [Fact]
    public void Never_asks_again_after_opting_out()
    {
        // Deliberately paired with a state that would otherwise sail through, so this can only pass
        // by honouring the opt-out rather than by tripping some other gate.
        var state = State(optedOut: true);

        var block = CreatePolicy().EvaluateWithoutEngagement(state, OldAccount, Now);

        Assert.Equal(DonationPromptBlock.OptedOut, block);
    }

    [Fact]
    public void Never_asks_someone_already_giving_monthly()
    {
        var block = CreatePolicy().EvaluateWithoutEngagement(State(hasActiveMonthly: true), OldAccount, Now);

        Assert.Equal(DonationPromptBlock.HasActiveMonthly, block);
    }

    [Fact]
    public void Leaves_a_recent_donor_alone_for_a_year()
    {
        var policy = CreatePolicy();

        var justInside = State(lastDonated: Now.AddDays(-364));
        var justOutside = State(lastDonated: Now.AddDays(-366));

        Assert.Equal(
            DonationPromptBlock.RecentlyDonated,
            policy.EvaluateWithoutEngagement(justInside, OldAccount, Now));

        Assert.Equal(
            DonationPromptBlock.None,
            policy.EvaluateWithoutEngagement(justOutside, OldAccount, Now));
    }

    [Fact]
    public void Does_not_ask_a_brand_new_account()
    {
        var thirteenDaysOld = Now.AddDays(-13);

        var block = CreatePolicy().EvaluateWithoutEngagement(State(), thirteenDaysOld, Now);

        Assert.Equal(DonationPromptBlock.AccountTooNew, block);
    }

    [Fact]
    public void Stops_for_good_at_the_lifetime_limit()
    {
        // Four asks used and the last one long enough ago that the cooldown cannot be what blocks it.
        var state = State(lastPrompted: Now.AddYears(-5), promptCount: 4);

        var block = CreatePolicy().EvaluateWithoutEngagement(state, OldAccount, Now);

        Assert.Equal(DonationPromptBlock.LifetimeLimitReached, block);
    }

    [Theory]
    // Gap owed after the nth ask, measured from that ask. Doubling, so 90, 180, 360.
    [InlineData(1, 89, DonationPromptBlock.WithinCooldown)]
    [InlineData(1, 91, DonationPromptBlock.None)]
    [InlineData(2, 179, DonationPromptBlock.WithinCooldown)]
    [InlineData(2, 181, DonationPromptBlock.None)]
    [InlineData(3, 359, DonationPromptBlock.WithinCooldown)]
    [InlineData(3, 361, DonationPromptBlock.None)]
    public void Doubles_the_gap_after_every_ask(int promptCount, int daysSince, DonationPromptBlock expected)
    {
        var state = State(lastPrompted: Now.AddDays(-daysSince), promptCount: promptCount);

        var block = CreatePolicy().EvaluateWithoutEngagement(state, OldAccount, Now);

        Assert.Equal(expected, block);
    }

    [Fact]
    public void Never_asks_more_than_four_times_in_under_two_years()
    {
        var policy = CreatePolicy();

        // Walk the whole sequence the way it would actually play out: ask, wait exactly the cooldown,
        // ask again. What matters is where it ends, not any single step.
        var state = State();
        var clock = OldAccount.AddDays(14);
        var asks = 0;
        var lastAskAt = clock;

        while (policy.EvaluateWithoutEngagement(state, OldAccount, clock) == DonationPromptBlock.None)
        {
            asks++;
            lastAskAt = clock;
            state = state with { LastPromptedAt = clock, PromptCount = asks };
            clock = clock.AddDays(policy.CooldownDaysAfter(asks));
        }

        var daysToLastAsk = (lastAskAt - OldAccount).TotalDays;

        Assert.Equal(4, asks);

        // 14 days to the first, then 90 + 180 + 360 between them. Asserted as a span rather than a
        // number so tuning any single setting shows up here as a changed lifetime, which is the
        // thing worth noticing.
        Assert.Equal(644, daysToLastAsk);
        Assert.True(daysToLastAsk < 730, $"The last ask should fall inside two years, fell on day {daysToLastAsk:F0}");
    }

    [Fact]
    public void Only_checks_engagement_until_it_has_been_reached()
    {
        var policy = CreatePolicy();

        Assert.True(policy.NeedsEngagementCheck(State()));
        Assert.False(policy.NeedsEngagementCheck(State(engagementReachedAt: Now.AddYears(-1))));
    }

    /// <summary>
    /// Binding a collection does not overwrite one that already has values in it, it appends to it.
    /// A default of four products plus four in appsettings.json therefore produced eight, and the
    /// prompt drew two rows of buttons. Exercised through the real binder rather than by
    /// constructing the settings directly, because the defect lives in the binding, not the type.
    /// </summary>
    [Fact]
    public void Configured_products_replace_the_defaults_instead_of_appending_to_them()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Donations:Products:0:ProductId"] = "support_small",
                    ["Donations:Products:0:Kind"] = "0",
                    ["Donations:Products:0:NominalAmountMinor"] = "500",
                    ["Donations:Products:1:ProductId"] = "support_monthly",
                    ["Donations:Products:1:Kind"] = "1",
                    ["Donations:Products:1:NominalAmountMinor"] = "300",
                    ["Donations:Products:1:BasePlanId"] = "monthly",
                })
            .Build();

        var settings = new DonationsSettings();
        configuration.GetSection(settings.SectionName).Bind(settings);

        Assert.Equal(
            new[] { "support_small", "support_monthly" },
            settings.ResolveProducts().Select(x => x.ProductId));
    }

    [Fact]
    public void Falls_back_to_default_products_when_none_are_configured()
    {
        var settings = new DonationsSettings();

        new ConfigurationBuilder().Build().GetSection(settings.SectionName).Bind(settings);

        Assert.NotEmpty(settings.ResolveProducts());
    }

    /// <summary>
    /// Play refuses to bill a subscription without knowing which base plan to bill, and the failure
    /// surfaces on the device rather than at startup, so a missing id would only be found by someone
    /// trying to give.
    /// </summary>
    [Fact]
    public void Every_monthly_product_names_a_base_plan()
    {
        var monthly = new DonationsSettings()
            .ResolveProducts()
            .Where(x => x.Kind == DonationKind.Monthly);

        Assert.All(monthly, x => Assert.False(string.IsNullOrWhiteSpace(x.BasePlanId)));
    }

    /// <summary>
    /// The catalogue is the only thing standing between the donation ledger and any other in-app
    /// product this app might ever sell, so an id that is not on it has to come back as nothing.
    /// </summary>
    [Fact]
    public void Catalog_only_recognises_configured_products()
    {
        var settings = new DonationsSettings();
        var catalog = new DonationCatalog(Options.Create(settings));

        var known = settings.ResolveProducts()[0].ProductId;

        Assert.NotNull(catalog.Find(known));
        Assert.Null(catalog.Find("some_other_product"));
    }

    [Fact]
    public void Cooldown_cannot_overflow_on_an_absurd_prompt_count()
    {
        // The lifetime limit means this cannot happen in practice; the clamp exists so a corrupt
        // count degrades into a very long wait instead of a negative one.
        var days = CreatePolicy().CooldownDaysAfter(int.MaxValue);

        Assert.True(days > 0);
    }
}
