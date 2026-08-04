namespace SplitServer.Responses;

public class GetConnectionStatusesResponse
{
    public required List<ConnectionStatusResponseItem> Statuses { get; init; }
}

public class ConnectionStatusResponseItem
{
    public required string UserId { get; init; }
    public required string Status { get; init; }

    /// <summary>
    /// Set only while a request is outstanding, so the receiver can accept it straight from the
    /// user picker without going to their notifications.
    /// </summary>
    public required string? ConnectionId { get; init; }
}

public static class ConnectionStatusValues
{
    public const string Connected = "connected";
    public const string PendingSent = "pending_sent";
    public const string PendingReceived = "pending_received";
    public const string None = "none";
}
