using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;

namespace SplitServer.Commands;

public class DeleteBudgetCommandHandler : IRequestHandler<DeleteBudgetCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IBudgetsRepository _budgetsRepository;

    public DeleteBudgetCommandHandler(
        IUsersRepository usersRepository,
        IBudgetsRepository budgetsRepository)
    {
        _usersRepository = usersRepository;
        _budgetsRepository = budgetsRepository;
    }

    public async Task<Result> Handle(DeleteBudgetCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }

        var budgetMaybe = await _budgetsRepository.GetById(command.BudgetId, ct);

        if (budgetMaybe.HasNoValue)
        {
            return Result.Failure($"Budget with id {command.BudgetId} was not found");
        }

        var budget = budgetMaybe.Value;

        if (budget.UserId != command.UserId)
        {
            return Result.Failure("User is not the owner of the budget");
        }

        return await _budgetsRepository.Delete(command.BudgetId, ct);
    }
}