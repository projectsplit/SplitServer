using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Commands;

public class DeleteNonGroupExpenseCommandHandler: IRequestHandler<DeleteNonGroupExpenseCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IExpensesRepository _expensesRepository;

    public DeleteNonGroupExpenseCommandHandler(
        IUsersRepository usersRepository,
        IExpensesRepository expensesRepository)
    {
        _usersRepository = usersRepository;
        _expensesRepository = expensesRepository;
    }

    public async Task<Result> Handle(DeleteNonGroupExpenseCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }

        var expenseMaybe = await _expensesRepository.GetById(command.ExpenseId, ct);

        if (expenseMaybe.HasNoValue)
        {
            return Result.Failure($"Expense with id {command.ExpenseId} was not found");
        }

        var expense = expenseMaybe.Value;

        if (expense is not NonGroupExpense nonGroupExpense)
        {
            return Result.Failure($"Expense with id {command.ExpenseId} was not found");
        }

        var userExists = nonGroupExpense.Payments.Any(p => p.UserId == command.UserId) ||
                         nonGroupExpense.Shares.Any(s => s.UserId == command.UserId);
        

        if (!userExists)
        {
            return Result.Failure("User must be an expense participant");
        }

        return await _expensesRepository.Delete(command.ExpenseId, ct);
    }
}