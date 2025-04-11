namespace SplitServer.Responses;

public class GetGroupExpensesResponse
{
    public required List<ExpenseResponseItem> Expenses { get; init; }
    public required string? Next { get; init; }
}