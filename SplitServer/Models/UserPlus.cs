using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SplitServer.Models;

/// <summary>
/// The mark on a user that says they have Buqs Plus. Present only while they do.
///
/// A copy, not the record: <see cref="PlusSubscription"/> holds what Google said, and this is
/// rewritten from those rows every time one of them changes. It lives on the user so that "does
/// this person have Plus" is answered by the read every request already makes, and so that it can
/// be seen by looking at the account.
///
/// Nothing may treat its mere presence as the answer. A subscription can run out between two
/// writes, so <see cref="ExpiresAt"/> is checked against the clock wherever this is read.
/// </summary>
[BsonIgnoreExtraElements]
public record UserPlus
{
    [BsonRepresentation(BsonType.String)]
    public required PlusPlan Plan { get; init; }

    /// <summary>When the paid period runs out. Past this, the mark is stale and counts for nothing.</summary>
    public required DateTime ExpiresAt { get; init; }

    /// <summary>False once renewal has been turned off: Plus ends at <see cref="ExpiresAt"/> rather than renewing.</summary>
    public required bool AutoRenewing { get; init; }

    /// <summary>Play's own word for the subscription's state, verbatim.</summary>
    public required string State { get; init; }

    /// <summary>The Play purchase token, which is the id of the <see cref="PlusSubscription"/> this was copied from.</summary>
    public required string SubscriptionId { get; init; }

    /// <summary>What Play will charge at the next renewal, in <see cref="RenewalCurrency"/>. Absent when Play does not say.</summary>
    public required decimal? RenewalPrice { get; init; }

    public required string? RenewalCurrency { get; init; }

    /// <summary>When the subscription this was copied from began.</summary>
    public required DateTime Since { get; init; }
}
