namespace SplitServer.Models;

public record Payment
{
    public required string MemberId { get; init; }
    public required decimal Amount { get; init; }
}