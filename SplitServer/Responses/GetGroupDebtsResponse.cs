using SplitServer.Models;

namespace SplitServer.Responses;

public class GetGroupDebtsResponse
{
    public required List<Debt> Debts { get; init; }
    public required Dictionary<string, Dictionary<string, decimal>> TotalSpent { get; init; }
}