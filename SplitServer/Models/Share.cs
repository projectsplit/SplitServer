namespace SplitServer.Models;

public record Share
{
    public required string MemberId { get; init; }
    public required decimal Amount { get; init; }
}