namespace SplitServer.Responses;

public class PersonalExpensesResponse
{
    public required List<PersonalExpenseResponseItem> Expenses { get; init; }
    public required string? Next { get; init; }
}