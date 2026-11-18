using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SplitServer.Configuration;
using SplitServer.Models;
using SplitServer.Services.Plus;

namespace SplitServer.Tests;

/// <summary>
/// Which Play product is Buqs Plus and which base plan is which billing period. Play will verify
/// any subscription bought against the app, so this lookup is the only thing that stops a monthly
/// tip, or a plan nobody told the server about, from being read as Plus.
/// </summary>
public class PlusCatalogTests
{
    private static PlusCatalog CreateCatalog(PlusSettings? settings = null) =>
        new(Options.Create(settings ?? new PlusSettings()));

    [Fact]
    public void Plus_is_not_for_sale_unless_it_is_switched_on()
    {
        Assert.False(CreateCatalog().IsEnabled);
        Assert.True(CreateCatalog(new PlusSettings { Enabled = true }).IsEnabled);
    }

    [Fact]
    public void Knows_the_two_plans_by_their_base_plan_ids()
    {
        var catalog = CreateCatalog();

        Assert.Equal(PlusPlan.Annual, catalog.FindPlan("annual"));
        Assert.Equal(PlusPlan.Monthly, catalog.FindPlan("monthly"));
    }

    /// <summary>
    /// A base plan added in the Play Console but not here must be refused, not taken for whichever
    /// of the two it most resembles.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("weekly")]
    [InlineData("Annual")]
    [InlineData("annual ")]
    [InlineData("annual-promo")]
    public void Refuses_a_base_plan_it_does_not_know(string? basePlanId)
    {
        Assert.Null(CreateCatalog().FindPlan(basePlanId));
    }

    [Fact]
    public void Follows_the_base_plan_ids_it_is_configured_with()
    {
        var catalog = CreateCatalog(new PlusSettings { MonthlyBasePlanId = "p1m", AnnualBasePlanId = "p1y" });

        Assert.Equal(PlusPlan.Monthly, catalog.FindPlan("p1m"));
        Assert.Equal(PlusPlan.Annual, catalog.FindPlan("p1y"));
        Assert.Null(catalog.FindPlan("monthly"));
    }

    /// <summary>The first plan is the one the app starts with selected.</summary>
    [Fact]
    public void Offers_the_annual_plan_first()
    {
        var plans = CreateCatalog().Plans;

        Assert.Equal(2, plans.Count);
        Assert.Equal(new PlusPlanOffer(PlusPlan.Annual, "annual"), plans[0]);
        Assert.Equal(new PlusPlanOffer(PlusPlan.Monthly, "monthly"), plans[1]);
    }

    [Fact]
    public void Names_the_plans_the_way_the_app_expects()
    {
        Assert.Equal("annual", PlusCatalog.NameOf(PlusPlan.Annual));
        Assert.Equal("monthly", PlusCatalog.NameOf(PlusPlan.Monthly));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("support_monthly")]
    [InlineData("support_monthly_500")]
    [InlineData("BUQS_PLUS")]
    [InlineData("buqs_plus_annual")]
    public void Only_the_one_product_is_Plus(string? productId)
    {
        Assert.False(CreateCatalog().IsPlusProduct(productId));
    }

    [Fact]
    public void Recognises_its_own_product()
    {
        Assert.True(CreateCatalog().IsPlusProduct("buqs_plus"));
        Assert.Equal("buqs_plus", CreateCatalog().ProductId);
    }

    // ---- Whether Plus is live ---------------------------------------------------------------

    /// <summary>
    /// Both, not either. Switched on with no way to verify a purchase would sell something the
    /// server could never grant, and credentials alone are not a decision to start selling.
    /// </summary>
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void Plus_is_live_only_when_switched_on_with_Play_to_verify_against(bool enabled, bool playIsConfigured, bool expected)
    {
        Assert.Equal(expected, CreateCatalog(new PlusSettings { Enabled = enabled }).IsOffered(playIsConfigured));
    }

    // ---- The free days and the cut-off ------------------------------------------------------

    private static PlusCatalog Configured(params (string Key, string? Value)[] values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(x => $"Plus:{x.Key}", x => x.Value))
            .Build();

        var settings = new PlusSettings();
        configuration.GetSection(settings.SectionName).Bind(settings);

        return CreateCatalog(settings);
    }

    [Fact]
    public void Out_of_the_box_there_are_thirty_free_days_and_two_notices_before_they_end()
    {
        var catalog = CreateCatalog();

        Assert.Equal(30, catalog.TrialDays);
        Assert.Equal(7, catalog.TrialReminderDays);
        Assert.Equal(1, catalog.TrialFinalReminderDays);
    }

    [Fact]
    public void Out_of_the_box_accounts_from_before_the_19th_of_November_2026_have_Plus_for_life()
    {
        var cutOff = CreateCatalog().LifetimeBefore;

        Assert.Equal(new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc), cutOff);
        Assert.Equal(DateTimeKind.Utc, cutOff?.Kind);
    }

    /// <summary>
    /// Configuration turns "…Z" into the machine's local time. Compared as it arrives against when
    /// an account was made, the cut-off would move by the server's offset from UTC, and which
    /// accounts have Plus for life would depend on where the server happens to run.
    /// </summary>
    [Theory]
    [InlineData("2026-11-19T00:00:00Z")]
    [InlineData("2026-11-19T02:00:00+02:00")]
    // Written with no zone at all, which it is documented as meaning UTC.
    [InlineData("2026-11-19T00:00:00")]
    [InlineData("2026-11-19")]
    public void The_cut_off_is_the_same_instant_wherever_the_server_runs(string configured)
    {
        var cutOff = Configured(("LifetimeBefore", configured)).LifetimeBefore;

        Assert.NotNull(cutOff);
        Assert.Equal(DateTimeKind.Utc, cutOff.Value.Kind);
        Assert.Equal(new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc).Ticks, cutOff.Value.Ticks);
    }

    /// <summary>
    /// Blanking the setting is the natural way to try to switch the rule off, and it does not work:
    /// binding leaves the built-in date alone when it is given nothing to put in its place. Pinned
    /// so that nobody relies on it, and so that the promise to the accounts from before the cut-off
    /// cannot be dropped by an empty line in a deploy script.
    /// </summary>
    [Fact]
    public void A_blank_cut_off_leaves_the_built_in_one_in_place()
    {
        Assert.Equal(
            new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc),
            Configured(("LifetimeBefore", "")).LifetimeBefore);
    }

    /// <summary>The way that does work: a cut-off no account can be older than.</summary>
    [Fact]
    public void A_cut_off_earlier_than_every_account_is_how_the_rule_is_switched_off_from_configuration()
    {
        var cutOff = Configured(("LifetimeBefore", "2000-01-01T00:00:00Z")).LifetimeBefore;

        Assert.Equal(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), cutOff);
    }

    [Fact]
    public void A_cut_off_set_to_nothing_in_code_switches_the_rule_off()
    {
        Assert.Null(CreateCatalog(new PlusSettings { LifetimeBefore = null }).LifetimeBefore);
    }

    [Fact]
    public void The_free_days_and_notices_follow_what_is_configured()
    {
        var catalog = Configured(("TrialDays", "14"), ("TrialReminderDays", "3"), ("TrialFinalReminderDays", "0"));

        Assert.Equal(14, catalog.TrialDays);
        Assert.Equal(3, catalog.TrialReminderDays);
        Assert.Equal(0, catalog.TrialFinalReminderDays);
    }

    [Fact]
    public void A_negative_number_of_free_days_means_none()
    {
        Assert.Equal(0, CreateCatalog(new PlusSettings { TrialDays = -3 }).TrialDays);
    }
}
