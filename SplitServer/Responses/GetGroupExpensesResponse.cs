using SplitServer.Models;

namespace SplitServer.Responses;

public class GetGroupExpensesResponse
{
    public required List<Expense> Expenses { get; init; }
    public required string? Next { get; init; }
}