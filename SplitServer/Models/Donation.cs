using MongoDB.Bson.Serialization.Attributes;

namespace SplitServer.Models;

/// <summary>
/// One payment that Stripe told us about. Stripe is the record of truth for the money; this exists
/// so the app can say "you have already given" without calling out, and so the totals can be added
/// up without an export.
/// </summary>
[BsonIgnoreExtraElements]
public record Donation : EntityBase
{
    /// <summary>
    /// Id is the Stripe object that identifies the payment: the Checkout Session for a one-off or
    /// the first month of a subscription, the Invoice for every renewal after that. Stripe retries
    /// webhooks and can deliver the same event twice, so keying on it makes a replay an overwrite
    /// of the row it already wrote rather than a second gift appearing out of nowhere.
    /// </summary>
    public required string? UserId { get; init; }

    public required long AmountMinor { get; init; }

    public required string Currency { get; init; }

    public required DonationKind Kind { get; init; }

    public required DonationStatus Status { get; init; }

    public required string? SubscriptionId { get; init; }
}
