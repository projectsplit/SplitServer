using SplitServer.Models;

namespace SplitServer.Dto;

public class GetGroupExpensesResponse
{
    public required List<Expense> Expenses { get; init; }
    public required string? Next { get; init; }
}