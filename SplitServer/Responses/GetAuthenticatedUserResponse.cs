using SplitServer.Models;

namespace SplitServer.Responses;

public class GetAuthenticatedUserResponse
{
    public required string UserId { get; init; }
    public required string Username { get; init; }
    public required bool HasNewerNotifications { get; init; }
    public required string Currency { get; init; }
    public required string TimeZone { get; init; }
    public required Coordinates TimeZoneCoordinates { get; init; }
    public required string? RecentGroupId { get; init; }
}