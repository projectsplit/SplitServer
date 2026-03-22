using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class DeletePersonalExpenseCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string ExpenseId { get; init; }
}