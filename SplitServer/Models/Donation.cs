using MongoDB.Bson.Serialization.Attributes;

namespace SplitServer.Models;

/// <summary>
/// One payment that Google Play told us about. Play is the record of truth for the money; this
/// exists so the app can say "you have already given" without calling out, and so the totals can be
/// added up without an export.
/// </summary>
[BsonIgnoreExtraElements]
public record Donation : EntityBase
{
    /// <summary>
    /// Id is whichever Play identifier stays put for the payment in question.
    ///
    /// For a one-off it is the purchase token, which survives the purchase moving from pending to
    /// paid; keying on the order id instead would write a second row the moment Play issued one.
    /// For a monthly gift it is the order id of that month's payment, because the purchase token is
    /// the same for the whole subscription and would collapse every renewal onto one row.
    ///
    /// Either way the key is Play's, not ours, so a redelivered notification overwrites the row it
    /// wrote the first time rather than a second gift appearing out of nowhere.
    /// </summary>
    public required string? UserId { get; init; }

    /// <summary>
    /// The purchase this belongs to. Recorded separately from <see cref="EntityBase.Id"/> because for
    /// renewals the two differ, and this is what a later notification arrives carrying.
    /// </summary>
    public required string PurchaseToken { get; init; }

    public required string ProductId { get; init; }

    /// <summary>Play's order id, which is what to search the Play Console for. Absent while a purchase is still pending.</summary>
    public required string? OrderId { get; init; }

    /// <summary>
    /// What this tier is nominally worth, in <see cref="Currency"/>'s minor unit. Not what was
    /// charged: Play prices per country and never reports the amount back through the purchase API,
    /// so the real figure lives in the Play Console against <see cref="OrderId"/>. This is here so
    /// the ledger can be added up in one currency.
    /// </summary>
    public required long AmountMinor { get; init; }

    public required string Currency { get; init; }

    public required DonationKind Kind { get; init; }

    public required DonationStatus Status { get; init; }

    /// <summary>The purchase token of the subscription that produced this, for monthly gifts only.</summary>
    public required string? SubscriptionId { get; init; }
}
