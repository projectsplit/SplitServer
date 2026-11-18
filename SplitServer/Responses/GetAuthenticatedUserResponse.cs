using SplitServer.Models;

namespace SplitServer.Responses;

public class GetAuthenticatedUserResponse
{
    public required string UserId { get; init; }
    public required string Username { get; init; }
    public required string? Email { get; init; }
    public required bool EmailVerified { get; init; }

    /// <summary>
    /// False for an account created through Google sign-in, which has no password to change.
    /// </summary>
    public required bool HasPassword { get; init; }
    public required bool HasNewerNotifications { get; init; }
    public required string Currency { get; init; }
    public required string TimeZone { get; init; }
    public required Coordinates TimeZoneCoordinates { get; init; }
    public required bool? ShowBudgetInfo { get; init; }
    public required string? RecentContextId { get; init; }
    public required bool PushNotificationsEnabled { get; init; }

    /// <summary>
    /// What access this account has to the Plus features. Always present. Sent with the user rather
    /// than fetched on its own because the app decides what to show or lock from it on every screen,
    /// and this is the one response it already has before it draws anything.
    /// </summary>
    public required PlusAccessResponse Plus { get; init; }
}