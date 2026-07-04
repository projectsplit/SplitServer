namespace SplitServer.Responses;

public class ConnectionRequestResponseItem
{
    public required string Id { get; init; }
    public required DateTime Created { get; init; }
    public required string SenderId { get; init; }
    public required string SenderUsername { get; init; }
}
