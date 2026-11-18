using MongoDB.Bson.Serialization.Attributes;

namespace SplitServer.Models;

/// <summary>
/// What a user has given, added up. Present only on someone who has given at least once.
///
/// A copy, not the record: the <see cref="Donation"/> rows are the ledger, and this is recomputed
/// from them after every write to it. It is set to the answer rather than added to, so a replayed
/// notification cannot count a gift twice and a refund takes its amount back out.
/// </summary>
[BsonIgnoreExtraElements]
public record UserDonations
{
    /// <summary>How many payments have arrived. A monthly gift counts once per month paid.</summary>
    public required int Count { get; init; }

    /// <summary>
    /// The nominal worth of every gift, in <see cref="Currency"/>'s minor unit, on the same terms as
    /// <see cref="Donation.AmountMinor"/>: what each tier is nominally worth, not what was charged.
    /// It is the one figure that exists for every gift and adds up in a single currency.
    /// </summary>
    public required long TotalMinor { get; init; }

    public required string Currency { get; init; }

    /// <summary>
    /// What was actually paid, per currency, for the gifts where Play reported it. Play charges in
    /// the buyer's own currency, so this is a list rather than one figure, and it can cover fewer
    /// gifts than <see cref="Count"/> when an order could not be read.
    /// </summary>
    public required List<ChargedTotal> Charged { get; init; }

    public required DateTime FirstDonatedAt { get; init; }

    public required DateTime LastDonatedAt { get; init; }
}

/// <summary>Money actually paid in one currency, tax included, as Play reported it order by order.</summary>
[BsonIgnoreExtraElements]
public record ChargedTotal
{
    public required string Currency { get; init; }

    public required decimal Amount { get; init; }
}
