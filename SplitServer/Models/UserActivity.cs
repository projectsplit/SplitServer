namespace SplitServer.Models;

public record UserActivity : EntityBase
{
    public required string? RecentGroupId { get; init; }
    public required DateTime? LastViewedNotificationTimestamp { get; init; }
}