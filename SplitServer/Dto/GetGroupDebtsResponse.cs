using SplitServer.Models;

namespace SplitServer.Dto;

public class GetGroupDebtsResponse
{
    public required List<Debt> Debts { get; init; }
}