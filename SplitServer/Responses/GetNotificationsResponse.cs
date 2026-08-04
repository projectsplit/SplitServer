namespace SplitServer.Responses;

public class GetNotificationsResponse
{
    public required List<NotificationResponseItem> Notifications { get; init; }
    public required string? Next { get; init; }
}
