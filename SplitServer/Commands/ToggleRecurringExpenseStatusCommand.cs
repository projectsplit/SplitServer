using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class ToggleRecurringExpenseStatusCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string RecurringExpenseId { get; init; }
}
