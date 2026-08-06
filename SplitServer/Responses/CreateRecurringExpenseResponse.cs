namespace SplitServer.Responses;

public class CreateRecurringExpenseResponse
{
    public required string RecurringExpenseId { get; init; }

    /// <summary>
    /// When the first expense will be created. Nothing exists yet, so this is what the client
    /// confirms back to the user in place of an expense appearing in a list.
    /// </summary>
    public required DateTime FirstOccurrence { get; init; }
}
