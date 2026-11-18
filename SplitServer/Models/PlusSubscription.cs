using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SplitServer.Models;

/// <summary>
/// One Buqs Plus subscription, keyed by the Play purchase token that identifies it. Everything on
/// it is what Google said the last time it was asked: the app's word is never written here.
///
/// The token is the key because it is all a renewal, a cancellation or a refund arrives carrying,
/// months after the purchase. Who it belongs to is therefore written down once, at purchase time,
/// and looked up afterwards rather than re-derived.
///
/// Someone can have more than one row over time — resubscribing after a lapse mints a new token —
/// so whether a person has Plus is never read off a single row. It is worked out from all of
/// theirs and copied onto <see cref="User.Plus"/>.
/// </summary>
[BsonIgnoreExtraElements]
public record PlusSubscription : EntityBase
{
    public required string UserId { get; init; }

    public required string ProductId { get; init; }

    /// <summary>Play's base plan id, kept verbatim next to the plan it was read as.</summary>
    public required string BasePlanId { get; init; }

    [BsonRepresentation(BsonType.String)]
    public required PlusPlan Plan { get; init; }

    /// <summary>
    /// Play's own word for the state, kept verbatim. A coarser flag would lose the difference
    /// between someone who cancelled, someone whose card is failing and someone who was refunded,
    /// and each of those wants a different answer. The one value that is ours rather than Play's is
    /// <c>SUBSCRIPTION_STATE_REPLACED</c>, for a token a newer purchase took over from.
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// When the paid period runs out. Plus lasts to here even after cancelling, and stops here when
    /// a payment fails for good.
    /// </summary>
    public required DateTime? ExpiresAt { get; init; }

    /// <summary>False once the subscriber has turned renewal off. They keep Plus until <see cref="ExpiresAt"/>.</summary>
    public required bool AutoRenewing { get; init; }

    /// <summary>
    /// What Play will charge at the next renewal, in <see cref="RenewalCurrency"/>. Play's figure in
    /// the subscriber's own currency, which is the only honest thing to quote back to them. Absent
    /// when Play does not say, as for a plan that will not renew.
    /// </summary>
    public required decimal? RenewalPrice { get; init; }

    public required string? RenewalCurrency { get; init; }

    /// <summary>Play's order id for the most recent payment, which is what to search the Play Console for.</summary>
    public required string? LatestOrderId { get; init; }

    public required DateTime? StartedAt { get; init; }

    /// <summary>
    /// The <see cref="ExpiresAt"/> a renewal reminder has already gone out for. Recording which
    /// renewal rather than a plain "sent" is what lets next year's reminder happen: the date moves
    /// on when the plan renews, and no longer matches.
    /// </summary>
    public required DateTime? RenewalReminderSentFor { get; init; }
}
