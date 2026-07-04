namespace SplitServer.Requests;

public class SubscribeToPushRequest
{
    public required string Endpoint { get; init; }
    public required string P256dh { get; init; }
    public required string Auth { get; init; }
}
