namespace SplitServer.Responses;

public class GetRecurringExpensesResponse
{
    public required List<RecurringExpenseResponseItem> RecurringExpenses { get; init; }
}
