namespace SplitServer.Responses;

public class NonGroupExpensesResponse
{
    public required List<NonGroupExpenseResponseItem> Expenses { get; init; }
    public required string? Next { get; init; }
}