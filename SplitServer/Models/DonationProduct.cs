namespace SplitServer.Models;

/// <summary>
/// One tier on offer. Play owns the price — it is set per country in the Play Console and the device
/// is told what it is — so the only things this side needs to know are which product ids are ours,
/// which of them renew, and roughly what each is worth for the ledger's benefit.
/// </summary>
public class DonationProduct
{
    /// <summary>Must match the in-app product id in the Play Console exactly.</summary>
    public required string ProductId { get; set; }

    public required DonationKind Kind { get; set; }

    /// <summary>
    /// What this tier is nominally worth, in the minor unit of
    /// <see cref="Configuration.DonationsSettings.NominalCurrency"/>. Not what anyone is charged:
    /// Play converts and rounds per country, and never reports the amount back through the purchase
    /// API. Recorded so the ledger can add up roughly without an export, and so tiers sort sensibly.
    /// </summary>
    public required long NominalAmountMinor { get; set; }

    /// <summary>
    /// Base plan id, for subscriptions only. Play needs it to know which plan of the subscription
    /// product to bill, and there is no default — a subscription with this unset cannot be bought.
    /// </summary>
    public string? BasePlanId { get; set; }
}
