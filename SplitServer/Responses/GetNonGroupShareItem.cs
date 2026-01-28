namespace SplitServer.Responses;

public class GetNonGroupShareItem
{
    public required string UserId { get; init; }
    public required string UserName { get; init; }
    public required decimal Amount { get; init; }
}