using MongoDB.Bson.Serialization.Attributes;

namespace SplitServer.Models;

/// <summary>
/// A monthly gift, keyed by the Play purchase token that identifies the subscription. Renewal
/// notifications arrive months after the purchase that started them and carry nothing but that
/// token, so who is paying is written down here at purchase time and looked up later rather than
/// re-derived from the notification.
/// </summary>
[BsonIgnoreExtraElements]
public record DonationSubscription : EntityBase
{
    public required string UserId { get; init; }

    public required string ProductId { get; init; }

    /// <summary>Nominal worth of the tier, on the same terms as <see cref="Donation.AmountMinor"/>.</summary>
    public required long AmountMinor { get; init; }

    public required string Currency { get; init; }

    /// <summary>False once Play reports the subscription is no longer paying, whoever ended it.</summary>
    public required bool IsActive { get; init; }

    /// <summary>
    /// Play's own word for the state, kept verbatim. Coarser flags lose the difference between
    /// someone who cancelled and someone whose card is failing, which is the only way to tell a
    /// deliberate stop from a fixable one when looking back at a row.
    /// </summary>
    public required string State { get; init; }

    /// <summary>When the current paid period runs out. Entitlement lasts to here even after cancellation.</summary>
    public required DateTime? ExpiresAt { get; init; }
}
