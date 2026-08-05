using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class DeleteRecurringExpenseCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string RecurringExpenseId { get; init; }
}
