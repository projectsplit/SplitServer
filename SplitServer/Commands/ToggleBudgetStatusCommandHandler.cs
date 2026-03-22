using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;

namespace SplitServer.Commands;

public class ToggleBudgetStatusCommandHandler : IRequestHandler<ToggleBudgetStatusCommand, Result>
{
    private readonly IBudgetsRepository _budgetsRepository;

    public ToggleBudgetStatusCommandHandler(IBudgetsRepository budgetsRepository)
    {
        _budgetsRepository = budgetsRepository;
    }

    public async Task<Result> Handle(ToggleBudgetStatusCommand command, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var budgets = await _budgetsRepository.GetAllByUserId(command.UserId, ct);

        var targetBudget = budgets.FirstOrDefault(b => b.Id == command.BudgetId);
        if (targetBudget == null)
        {
            return Result.Failure("Budget not found");
        }

        if (targetBudget.IsActive)
        {
            // Deactivate it
            var deactivatedBudget = targetBudget with { IsActive = false, Updated = now };
            await _budgetsRepository.Update(deactivatedBudget, ct);
        }
        else
        {
            // Activate it and deactivate others
            foreach (var budget in budgets.Where(b => b.IsActive))
            {
                var deactivatedBudget = budget with { IsActive = false, Updated = now };
                await _budgetsRepository.Update(deactivatedBudget, ct);
            }

            var activatedBudget = targetBudget with { IsActive = true, Updated = now };
            await _budgetsRepository.Update(activatedBudget, ct);
        }

        return Result.Success();
    }
}