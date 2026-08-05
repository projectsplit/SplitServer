using SplitServer.Models;

namespace SplitServer.Requests;

public class CreateRecurringExpenseRequest
{
    /// <summary>Set for a group recurrence, null for non-group and personal ones.</summary>
    public string? GroupId { get; init; }

    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string Description { get; init; }
    public required RecurrenceSchedule Schedule { get; init; }
    public List<GroupPayment>? Payments { get; init; }
    public List<GroupShare>? Shares { get; init; }
    public List<Payment>? NonGroupPayments { get; init; }
    public List<Share>? NonGroupShares { get; init; }
    public required List<LabelRequestItem> Labels { get; init; }
    public required Location? Location { get; init; }
}
