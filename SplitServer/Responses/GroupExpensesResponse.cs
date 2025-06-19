namespace SplitServer.Responses;

public class GroupExpensesResponse
{
    public required List<ExpenseResponseItem> Expenses { get; init; }
    public required string? Next { get; init; }
}