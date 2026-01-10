namespace SplitServer.Responses;

public class GroupExpensesResponse
{
    public required List<GroupExpenseResponseItem> Expenses { get; init; }
    public required string? Next { get; init; }
}