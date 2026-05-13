namespace SplitServer.Models;

public record Payment
{
    public required string UserId { get; init; }
    public required decimal Amount { get; init; }
}