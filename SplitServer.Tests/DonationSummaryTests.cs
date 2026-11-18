using SplitServer.Models;
using SplitServer.Services.Donations;

namespace SplitServer.Tests;

/// <summary>
/// The total kept on each user of what they have given. It is recomputed from the ledger rather
/// than added to, and these pin the properties that only hold because of that: a replay cannot
/// count a gift twice, and a refund comes back out without anything subtracting it.
/// </summary>
public class DonationSummaryTests
{
    private static readonly DateTime Now = new(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);

    private static Donation Gift(
        string id,
        long amountMinor,
        DonationStatus status = DonationStatus.Succeeded,
        DateTime? created = null,
        decimal? chargedAmount = null,
        string? chargedCurrency = null,
        DonationKind kind = DonationKind.OneTime) =>
        new()
        {
            Id = id,
            Created = created ?? Now,
            Updated = Now,
            UserId = "user-1",
            PurchaseToken = id,
            ProductId = "support_small",
            OrderId = $"GPA.{id}",
            AmountMinor = amountMinor,
            Currency = "usd",
            Kind = kind,
            Status = status,
            SubscriptionId = null,
            ChargedAmount = chargedAmount,
            ChargedCurrency = chargedCurrency,
        };

    [Fact]
    public void Someone_who_has_never_given_has_no_summary()
    {
        Assert.Null(DonationSummary.From([], "usd"));
    }

    /// <summary>Money that has not arrived, or never did, is not a gift yet.</summary>
    [Fact]
    public void Gifts_that_have_not_arrived_do_not_make_a_summary()
    {
        var donations = new[]
        {
            Gift("a", 500, DonationStatus.Pending),
            Gift("b", 1200, DonationStatus.Failed),
        };

        Assert.Null(DonationSummary.From(donations, "usd"));
    }

    [Fact]
    public void Adds_up_everything_that_arrived()
    {
        var donations = new[]
        {
            Gift("a", 500, created: Now.AddDays(-30)),
            Gift("b", 1200, created: Now.AddDays(-10)),
            Gift("c", 5000, created: Now.AddDays(-20)),
        };

        var summary = DonationSummary.From(donations, "usd");

        Assert.NotNull(summary);
        Assert.Equal(3, summary.Count);
        Assert.Equal(6700, summary.TotalMinor);
        Assert.Equal("usd", summary.Currency);
        Assert.Equal(Now.AddDays(-30), summary.FirstDonatedAt);
        Assert.Equal(Now.AddDays(-10), summary.LastDonatedAt);
    }

    [Fact]
    public void Only_counts_what_arrived_among_what_did_not()
    {
        var donations = new[]
        {
            Gift("a", 500),
            Gift("b", 1200, DonationStatus.Pending),
            Gift("c", 5000, DonationStatus.Failed),
        };

        var summary = DonationSummary.From(donations, "usd");

        Assert.NotNull(summary);
        Assert.Equal(1, summary.Count);
        Assert.Equal(500, summary.TotalMinor);
    }

    /// <summary>
    /// A refund reaches the ledger as the gift's row turning failed. Nothing subtracts anything:
    /// the summary is simply computed again, and the refunded gift is no longer in it.
    /// </summary>
    [Fact]
    public void A_refund_takes_its_amount_back_out()
    {
        var before = DonationSummary.From(
            [Gift("a", 500, chargedAmount: 4.49m, chargedCurrency: "GBP"), Gift("b", 1200, chargedAmount: 10.99m, chargedCurrency: "GBP")],
            "usd");

        var after = DonationSummary.From(
            [Gift("a", 500, chargedAmount: 4.49m, chargedCurrency: "GBP"), Gift("b", 1200, DonationStatus.Failed, chargedAmount: 10.99m, chargedCurrency: "GBP")],
            "usd");

        Assert.NotNull(before);
        Assert.Equal(1700, before.TotalMinor);
        Assert.Equal(15.48m, Assert.Single(before.Charged).Amount);

        Assert.NotNull(after);
        Assert.Equal(1, after.Count);
        Assert.Equal(500, after.TotalMinor);
        Assert.Equal(4.49m, Assert.Single(after.Charged).Amount);
    }

    [Fact]
    public void Refunding_the_only_gift_removes_the_summary_altogether()
    {
        Assert.Null(DonationSummary.From([Gift("a", 500, DonationStatus.Failed)], "usd"));
    }

    /// <summary>
    /// The same rows give the same answer however many times it is asked, which is what makes it
    /// safe to recompute after every notification Play redelivers.
    /// </summary>
    [Fact]
    public void Computing_it_again_changes_nothing()
    {
        var donations = new[] { Gift("a", 500, chargedAmount: 4.49m, chargedCurrency: "GBP"), Gift("b", 1200) };

        var first = DonationSummary.From(donations, "usd");
        var second = DonationSummary.From(donations, "usd");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Count, second.Count);
        Assert.Equal(first.TotalMinor, second.TotalMinor);
        Assert.Equal(first.Charged, second.Charged);
    }

    /// <summary>
    /// Play charges each buyer in the currency of their Play country, so what was really paid is
    /// kept per currency rather than forced into one figure at some exchange rate.
    /// </summary>
    [Fact]
    public void Keeps_what_was_actually_paid_per_currency()
    {
        var donations = new[]
        {
            Gift("a", 500, chargedAmount: 4.49m, chargedCurrency: "GBP"),
            Gift("b", 500, chargedAmount: 5.49m, chargedCurrency: "EUR"),
            Gift("c", 1200, chargedAmount: 10.99m, chargedCurrency: "gbp"),
        };

        var summary = DonationSummary.From(donations, "usd");

        Assert.NotNull(summary);
        var expected = new List<ChargedTotal>
        {
            new() { Currency = "EUR", Amount = 5.49m },
            new() { Currency = "GBP", Amount = 15.48m },
        };

        Assert.Equal(expected, summary.Charged);
    }

    /// <summary>
    /// The real amount is looked up separately and can be missing. Such a gift still counts in the
    /// nominal total, which exists for every row, and is simply absent from the charged ones.
    /// </summary>
    [Fact]
    public void A_gift_whose_real_amount_is_unknown_still_counts()
    {
        var donations = new[]
        {
            Gift("a", 500, chargedAmount: 4.49m, chargedCurrency: "GBP"),
            Gift("b", 1200),
            Gift("c", 5000, chargedAmount: 44.99m, chargedCurrency: null),
        };

        var summary = DonationSummary.From(donations, "usd");

        Assert.NotNull(summary);
        Assert.Equal(3, summary.Count);
        Assert.Equal(6700, summary.TotalMinor);
        Assert.Equal(new ChargedTotal { Currency = "GBP", Amount = 4.49m }, Assert.Single(summary.Charged));
    }

    [Fact]
    public void A_gift_with_no_real_amounts_at_all_has_an_empty_charged_list()
    {
        var summary = DonationSummary.From([Gift("a", 500)], "usd");

        Assert.NotNull(summary);
        Assert.Empty(summary.Charged);
    }

    [Fact]
    public void Every_month_of_a_monthly_gift_counts_as_its_own_payment()
    {
        var donations = new[]
        {
            Gift("GPA.1", 300, kind: DonationKind.Monthly, created: Now.AddMonths(-2)),
            Gift("GPA.1..0", 300, kind: DonationKind.Monthly, created: Now.AddMonths(-1)),
            Gift("GPA.1..1", 300, kind: DonationKind.Monthly, created: Now),
        };

        var summary = DonationSummary.From(donations, "usd");

        Assert.NotNull(summary);
        Assert.Equal(3, summary.Count);
        Assert.Equal(900, summary.TotalMinor);
    }
}
