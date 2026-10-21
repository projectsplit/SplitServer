namespace SplitServer.Responses;

public record GetExpensesForExportResponse
{
    /// <summary>The user's preferred currency, which the converted figures are in.</summary>
    public required string Currency { get; init; }

    public required string TimeZone { get; init; }

    /// <summary>Oldest first, so the report reads as a ledger.</summary>
    public required List<ExportExpenseItem> Expenses { get; init; }
}

/// <summary>
/// One expense with every id already turned into something a person can read. A file leaves the
/// app, so nothing in it can be resolved later the way the screens resolve member ids.
/// </summary>
public record ExportExpenseItem
{
    public required string Id { get; init; }
    public required DateTime Occurred { get; init; }
    public required string Description { get; init; }
    public required ExpenseResponseType Type { get; init; }
    public string? GroupId { get; init; }
    public string? GroupName { get; init; }
    public required string Currency { get; init; }

    /// <summary>The whole expense, whoever paid it.</summary>
    public required decimal Amount { get; init; }

    /// <summary>The user's part of it. Zero when they only paid for other people.</summary>
    public required decimal YourShare { get; init; }

    /// <summary>What the user put in towards it. Zero when someone else covered them.</summary>
    public required decimal YouPaid { get; init; }

    /// <summary>
    /// The same two figures in the user's preferred currency, at the rate of the day the expense
    /// happened. Null when no rates could be loaded, so the report can still be produced without them.
    /// </summary>
    public decimal? YourShareConverted { get; init; }

    public decimal? YouPaidConverted { get; init; }

    public required List<ExportParticipant> PaidBy { get; init; }
    public required List<ExportParticipant> SplitWith { get; init; }
    public required List<string> Labels { get; init; }
    public string? Place { get; init; }
    public required bool IsRecurring { get; init; }
}

public record ExportParticipant
{
    public required string Name { get; init; }

    /// <summary>
    /// Whether this is the person the report is for, so the file can say "you" instead of their own
    /// username back to them. Decided here because only the server knows which member ids are theirs.
    /// </summary>
    public required bool IsYou { get; init; }

    public required decimal Amount { get; init; }
}
