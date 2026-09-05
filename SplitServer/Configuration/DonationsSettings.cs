using SplitServer.Models;

namespace SplitServer.Configuration;

/// <summary>
/// What to offer, and how rarely to ask. Every number that decides whether a person sees the
/// donation prompt lives here so the cadence can be loosened or tightened without a deploy of new
/// logic, and so the policy can be read in one place rather than inferred from scattered checks.
/// </summary>
public class DonationsSettings : ISettings
{
    public string SectionName { get; init; } = "Donations";

    private static readonly DonationProduct[] DefaultProducts =
    [
        new() { ProductId = "support_small", Kind = DonationKind.OneTime, NominalAmountMinor = 500 },
        new() { ProductId = "support_medium", Kind = DonationKind.OneTime, NominalAmountMinor = 1200 },
        new() { ProductId = "support_large", Kind = DonationKind.OneTime, NominalAmountMinor = 2500 },
        new() { ProductId = "support_monthly", Kind = DonationKind.Monthly, NominalAmountMinor = 300, BasePlanId = "monthly" },
    ];

    /// <summary>
    /// The tiers on offer, each one an in-app product that must also exist in the Play Console under
    /// the same id. Read through <see cref="ResolveProducts"/> rather than directly.
    /// </summary>
    /// <remarks>
    /// Empty by default, and it has to stay that way. Configuration binding does not replace a
    /// collection that already holds values — it reads the current one through this property, copies
    /// it, and appends whatever is configured on the end, so an inline default plus a configured
    /// list yields both. Every scalar setting on this class can carry an inline default safely; a
    /// collection cannot.
    /// </remarks>
    public DonationProduct[] Products { get; set; } = [];

    /// <summary>
    /// The products to actually offer: whatever is configured, or the built-in defaults if the
    /// setting is absent. A method rather than a property so the config binder never sees it.
    /// </summary>
    public DonationProduct[] ResolveProducts() =>
        Products.Length > 0
            // Two entries sharing a product id would collide on their React key in the client and
            // make the ledger's product lookup ambiguous.
            ? Products.DistinctBy(x => x.ProductId).ToArray()
            : DefaultProducts;

    /// <summary>
    /// ISO currency the nominal amounts below are expressed in. Play charges in the buyer's own
    /// currency at prices set per country in the Play Console, so this is not what anyone pays — it
    /// is the yardstick the ledger records tiers against. What the buyer actually sees is the
    /// localised price string Play returns on the device.
    /// </summary>
    public string NominalCurrency { get; set; } = "usd";

    /// <summary>
    /// How long an account must exist before it is ever asked. Asking someone who has not yet got
    /// anything out of the app reads as a paywall, which is the opposite of what this is.
    /// </summary>
    public int MinAccountAgeDays { get; set; } = 14;

    /// <summary>
    /// Expenses the person must have created before being asked. Account age alone lets in someone
    /// who signed up and never came back; this is the evidence that the app is actually useful to them.
    /// </summary>
    public int MinExpensesCreated { get; set; } = 15;

    /// <summary>
    /// Gap before the second ask. Each later ask doubles it, so the sequence is 90, 180, 360 days —
    /// someone who keeps saying no is asked progressively less rather than on a fixed drumbeat.
    /// </summary>
    public int FirstCooldownDays { get; set; } = 90;

    /// <summary>
    /// Hard ceiling on how many times one person is ever asked, no matter how long they stay. After
    /// this the prompt is done with them for good and the settings entry is the only way in.
    /// </summary>
    public int MaxLifetimePrompts { get; set; } = 4;

    /// <summary>How long someone who has given is left alone. Anyone with a live monthly gift is never asked at all.</summary>
    public int PostDonationCooldownDays { get; set; } = 365;
}
