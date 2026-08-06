namespace SplitServer.Responses;

public class ProcessDueRecurringExpensesResponse
{
    public required int Processed { get; init; }
    public required int Created { get; init; }
    public required int Failed { get; init; }
}
