namespace SplitServer.Requests;

/// <summary>Shared by the actions that only need to name a template: delete and pause/resume.</summary>
public class RecurringExpenseRequest
{
    public required string RecurringExpenseId { get; init; }
}
