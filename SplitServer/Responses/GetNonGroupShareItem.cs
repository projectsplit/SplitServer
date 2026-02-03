namespace SplitServer.Responses;

public class GetNonGroupShareItem
{
    public required string UserId { get; init; }
    public required string Username { get; init; }
    public required decimal Amount { get; init; }
}