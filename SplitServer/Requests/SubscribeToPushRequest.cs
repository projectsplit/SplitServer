using SplitServer.Models;

namespace SplitServer.Requests;

public class SubscribeToPushRequest
{
    public required string Endpoint { get; init; }

    /// <summary>
    /// Absent from browser clients that predate the Android app, and absent is exactly what they are:
    /// Web Push, the enum's zero value. Android sends Fcm explicitly.
    /// </summary>
    public PushDeviceKind Kind { get; init; } = PushDeviceKind.WebPush;

    public string? P256dh { get; init; }
    public string? Auth { get; init; }
}
