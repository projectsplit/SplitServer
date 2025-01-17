using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class DeleteExpenseCommand : IRequest<Result>
{
    public string UserId { get; }
    public string ExpenseId { get; }

    public DeleteExpenseCommand(
        string userId,
        string expenseId)
    {
        UserId = userId;
        ExpenseId = expenseId;
    }
}