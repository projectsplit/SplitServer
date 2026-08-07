using MongoDB.Bson.Serialization.Attributes;

namespace SplitServer.Models;

/// <summary>
/// A monthly gift, keyed by the Stripe subscription id. Renewal invoices arrive months after the
/// checkout that started them and carry no reliable trace of who is paying, so the link is written
/// down here at checkout time and looked up later rather than read back off the invoice.
/// </summary>
[BsonIgnoreExtraElements]
public record DonationSubscription : EntityBase
{
    public required string UserId { get; init; }

    public required long AmountMinor { get; init; }

    public required string Currency { get; init; }

    /// <summary>False once Stripe reports the subscription ended, whoever ended it.</summary>
    public required bool IsActive { get; init; }
}
