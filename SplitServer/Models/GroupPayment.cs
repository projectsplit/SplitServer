namespace SplitServer.Models;

public record GroupPayment
{
    public required string MemberId { get; init; }
    public required decimal Amount { get; init; }
}