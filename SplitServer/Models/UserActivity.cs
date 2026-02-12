namespace SplitServer.Models;

public record UserActivity : EntityBase
{
    public required string? RecentContextId { get; init; }
    public required DateTime? LastViewedNotificationTimestamp { get; init; }
}