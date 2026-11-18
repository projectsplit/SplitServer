using MongoDB.Bson.Serialization.Attributes;

namespace SplitServer.Models;

/// <summary>
/// The moment Buqs Plus went live on this database. One document, written once and never again.
///
/// It exists because the free days are counted from it. An account older than Plus starts its days
/// here rather than at its own creation, so that nobody's are used up before there was anything to
/// use them on — and that only holds if the moment is the same every time it is asked for, across
/// restarts and across instances. Hence a document, not something worked out afresh on each boot.
/// </summary>
[BsonIgnoreExtraElements]
public record PlusLaunch : EntityBase
{
    /// <summary>The id of the one document. There is no second.</summary>
    public const string DocumentId = "launch";

    public required DateTime LiveSince { get; init; }
}
