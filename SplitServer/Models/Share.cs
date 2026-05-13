namespace SplitServer.Models;

public record Share
{
    public required string UserId { get; init; }
    public required decimal Amount { get; init; }
}