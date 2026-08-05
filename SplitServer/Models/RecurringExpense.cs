using SplitServer.Requests;

namespace SplitServer.Models;

/// <summary>
/// A template that materializes into a real expense once per cycle. Deliberately holds the request
/// shaped payload rather than a finished expense: every occurrence goes through the same create
/// command a user would, so validation, label creation and notifications stay in one place.
/// </summary>
public abstract record RecurringExpense : EntityBase
{
    /// <summary>Account that owns the template. Always the creator, never a member id.</summary>
    public required string UserId { get; init; }

    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string Description { get; init; }
    public required Location? Location { get; init; }
    public required List<LabelRequestItem> Labels { get; init; }

    public required RecurrenceSchedule Schedule { get; init; }

    /// <summary>
    /// Time zone the schedule is read in, captured when the template was created. The chosen day
    /// and time mean what they meant to the user, wherever they happen to open the app later.
    /// </summary>
    public required string TimeZoneId { get; init; }

    /// <summary>
    /// First occurrence the schedule resolved to, in UTC. Kept for display — the series never
    /// steps from it, so a clamped short month cannot drag later occurrences with it.
    /// </summary>
    public required DateTime AnchorDate { get; init; }

    /// <summary>When the next expense is due, in UTC.</summary>
    public required DateTime NextOccurrence { get; init; }

    public required bool IsPaused { get; init; }

    /// <summary>Expense produced by the most recent run. What "jump to latest" navigates to.</summary>
    public required string? LastExpenseId { get; init; }

    public required DateTime? LastRunAt { get; init; }

    /// <summary>
    /// Why the last run refused to produce an expense, if it did. Set alongside a pause so the user
    /// can see a template stopped because the group was deleted or a connection was revoked,
    /// instead of it silently failing forever.
    /// </summary>
    public required string? LastError { get; init; }
}

public record GroupRecurringExpense : RecurringExpense
{
    public required string GroupId { get; init; }
    public required List<GroupPayment> Payments { get; init; }
    public required List<GroupShare> Shares { get; init; }
}

public record NonGroupRecurringExpense : RecurringExpense
{
    public required List<Payment> Payments { get; init; }
    public required List<Share> Shares { get; init; }
}

public record PersonalRecurringExpense : RecurringExpense;
