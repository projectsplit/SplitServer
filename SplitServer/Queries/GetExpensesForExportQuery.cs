using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Responses;

namespace SplitServer.Queries;

/// <summary>
/// Everything the user spent in a window, ready to be written out as a report. Unpaginated on
/// purpose: a file has to hold the whole period, and the window is what keeps it bounded.
/// </summary>
public class GetExpensesForExportQuery : IRequest<Result<GetExpensesForExportResponse>>
{
    public required string UserId { get; init; }

    /// <summary>
    /// Calendar days in the user's own time zone. The window runs from the start of the first to the
    /// end of the last, the way a budget cycle does.
    /// </summary>
    public required DateTime StartDate { get; init; }

    public required DateTime EndDate { get; init; }

    /// <summary>Which kinds of expense to include. The same flags a budget is scoped with.</summary>
    public required BudgetScope Scope { get; init; }

    /// <summary>Which groups, when the scope includes groups. Empty means all of them.</summary>
    public required List<string> TargetGroupIds { get; init; }
}
