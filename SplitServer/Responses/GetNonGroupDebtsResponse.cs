using SplitServer.Models;
namespace SplitServer.Responses;

public class GetNonGroupDebtsResponse  
{
    public required List<NonGroupDebt> Debts { get; init; }
    public required Dictionary<string, Dictionary<string, decimal>> TotalSpent { get; init; }
    public required Dictionary<string, decimal> ConvertedTotalSpent { get; init; }
    public required Dictionary<string, Dictionary<string, decimal>> TotalSent { get; init; }
    public required Dictionary<string, Dictionary<string, decimal>> TotalReceived { get; init; }
}