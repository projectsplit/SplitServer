using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class ToggleBudgetStatusCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string BudgetId { get; init; }
}
