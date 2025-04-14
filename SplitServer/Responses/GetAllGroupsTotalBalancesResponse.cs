namespace SplitServer.Responses;

public class GetAllGroupsTotalBalancesResponse
{
    public required Dictionary<string, decimal> Balances { get; init; }
    public required int GroupCount { get; init; }
    public required decimal ConvertedBalance { get; init; }
}