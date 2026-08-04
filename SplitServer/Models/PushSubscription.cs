namespace SplitServer.Models;

public record PushSubscription : EntityBase
{
    public required string UserId { get; init; }
    public required string Endpoint { get; init; }
    public required string P256dh { get; init; }
    public required string Auth { get; init; }
}
