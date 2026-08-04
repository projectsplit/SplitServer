namespace SplitServer.Responses;

public class NotificationResponseItem
{
    public required string Id { get; init; }
    public required DateTime Created { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public required string? Url { get; init; }
}
