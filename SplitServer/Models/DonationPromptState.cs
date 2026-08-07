using MongoDB.Bson.Serialization.Attributes;

namespace SplitServer.Models;

/// <summary>
/// Everything the server needs to decide whether this person should see the donation prompt, keyed
/// by user id. Kept as one small document so the decision is a single read on a path that gets
/// asked on every app load, and kept server-side rather than in the browser so clearing storage or
/// signing in on a second device does not start the asking over.
/// </summary>
[BsonIgnoreExtraElements]
public record DonationPromptState : EntityBase
{
    public required DateTime? LastPromptedAt { get; init; }

    public required int PromptCount { get; init; }

    public required DateTime? LastDonatedAt { get; init; }

    /// <summary>"Don't ask again". Permanent, and nothing but the person themselves can clear it.</summary>
    public required bool OptedOut { get; init; }

    /// <summary>
    /// When this account first cleared the "has actually used the app" bar. Counting expenses is the
    /// one expensive part of the decision, so the answer is written down the first time it comes back
    /// true and never asked again — the bar is about having got value from the app, and that does not
    /// stop being true later.
    /// </summary>
    public required DateTime? EngagementReachedAt { get; init; }

    /// <summary>
    /// Mirrors whether any <see cref="DonationSubscription"/> for this user is still active. The
    /// subscription document is the source of truth; this copy exists so the prompt decision stays a
    /// single read, and the webhook writes both. Set to a value rather than incremented, so a
    /// redelivered event lands on the same answer.
    /// </summary>
    public required bool HasActiveMonthly { get; init; }

    public static DonationPromptState CreateEmpty(string userId, DateTime now) =>
        new()
        {
            Id = userId,
            Created = now,
            Updated = now,
            LastPromptedAt = null,
            PromptCount = 0,
            LastDonatedAt = null,
            OptedOut = false,
            EngagementReachedAt = null,
            HasActiveMonthly = false,
        };
}
