namespace SplitServer.Models;

public class Payment
{
    public required string MemberId { get; init; }
    public required decimal Amount { get; init; }
}