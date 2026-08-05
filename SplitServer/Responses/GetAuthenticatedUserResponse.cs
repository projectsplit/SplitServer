using SplitServer.Models;

namespace SplitServer.Responses;

public class GetAuthenticatedUserResponse
{
    public required string UserId { get; init; }
    public required string Username { get; init; }
    public required string? Email { get; init; }
    public required bool EmailVerified { get; init; }
    public required bool HasNewerNotifications { get; init; }
    public required string Currency { get; init; }
    public required string TimeZone { get; init; }
    public required Coordinates TimeZoneCoordinates { get; init; }
    public required bool? ShowBudgetInfo { get; init; }
    public required string? RecentContextId { get; init; }
    public required bool PushNotificationsEnabled { get; init; }

    /// <summary>Drives whether the settings menu offers "Manage recurring expenses" at all.</summary>
    public required bool HasRecurringExpenses { get; init; }
}