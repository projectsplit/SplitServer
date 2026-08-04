namespace SplitServer.Models;

/// <summary>
/// An entry in a user's in-app activity feed. Written for every recipient of a notification,
/// independently of whether that user has push enabled — the feed is the record, push is only
/// one way of delivering it.
/// </summary>
public record Notification : EntityBase
{
    public required string UserId { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public required string? Url { get; init; }
}
