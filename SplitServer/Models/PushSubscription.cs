namespace SplitServer.Models;

public record PushSubscription : EntityBase
{
    public required string UserId { get; init; }

    /// <summary>
    /// The address of one device install. For a browser that is the push service URL; for the Android
    /// app it is the FCM registration token. They are different strings from different systems, but
    /// they play the same role — unique per install, rotated by the platform, and the thing a
    /// re-subscribe replaces — so both live here and the subscribe path stays one path.
    /// </summary>
    public required string Endpoint { get; init; }

    public required PushDeviceKind Kind { get; init; }

    /// <summary>Web Push payload encryption key. Null for FCM, which encrypts in transit itself.</summary>
    public string? P256dh { get; init; }

    /// <summary>Web Push auth secret. Null for FCM.</summary>
    public string? Auth { get; init; }
}
