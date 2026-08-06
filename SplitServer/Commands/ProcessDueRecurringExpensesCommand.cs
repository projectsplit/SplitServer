using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Commands;

public class ProcessDueRecurringExpensesCommand : IRequest<Result<ProcessDueRecurringExpensesResponse>>
{
    /// <summary>Caps one pass so a large backlog is drained over several ticks, not one long one.</summary>
    public int BatchSize { get; init; } = 200;
}
