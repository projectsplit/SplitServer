using SplitServer.Models;
using SplitServer.Requests;

namespace SplitServer.Responses;

public record RecurringExpenseResponseItem
{
    public required string Id { get; init; }
    public required DateTime Created { get; init; }
    public required DateTime Updated { get; init; }
    public required ExpenseResponseType TransactionType { get; init; }
    public required string? GroupId { get; init; }

    /// <summary>Resolved for the manage list, which shows templates from every scope side by side.</summary>
    public required string? GroupName { get; init; }

    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string Description { get; init; }
    public required Location? Location { get; init; }
    public required List<LabelRequestItem> Labels { get; init; }
    /// <summary>Null when the stored template has no readable schedule; the row is then unrunnable.</summary>
    public required RecurrenceSchedule? Schedule { get; init; }

    /// <summary>The first occurrence the schedule resolved to when the template was created.</summary>
    public required DateTime AnchorDate { get; init; }
    public required DateTime NextOccurrence { get; init; }
    public required bool IsPaused { get; init; }
    public required string? LastError { get; init; }

    /// <summary>
    /// The zone the schedule's wall clock fields are read in. Exposed so a client whose user has
    /// since moved zones can tell that "at 09:00" means 09:00 somewhere else.
    /// </summary>
    public required string TimeZoneId { get; init; }

    /// <summary>
    /// When the template last produced — or last tried to produce — an expense. Unlike
    /// LastExpenseId this survives the expense being deleted, so it is the truthful "has this ever
    /// run" signal.
    /// </summary>
    public required DateTime? LastRunAt { get; init; }

    public required List<GroupPayment>? Payments { get; init; }
    public required List<GroupShare>? Shares { get; init; }
    public required List<Payment>? NonGroupPayments { get; init; }
    public required List<Share>? NonGroupShares { get; init; }

    /// <summary>
    /// Identity of the most recent occurrence. Occurred and Created together are what the client
    /// turns into a jump token, so tapping the row lands on that exact expense in its own list.
    /// </summary>
    public required string? LastExpenseId { get; init; }

    public required DateTime? LastExpenseOccurred { get; init; }
    public required DateTime? LastExpenseCreated { get; init; }
}
