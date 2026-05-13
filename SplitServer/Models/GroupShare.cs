namespace SplitServer.Models;

public record GroupShare
{
    public required string MemberId { get; init; }
    public required decimal Amount { get; init; }
}