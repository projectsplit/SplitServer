namespace SplitServer.Responses;

public class GetNonGroupPaymentItem
{
    public required string UserId { get; init; }
    public required string UserName { get; init; }
    public required decimal Amount { get; init; }
}