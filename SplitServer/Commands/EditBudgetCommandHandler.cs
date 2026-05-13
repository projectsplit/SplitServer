using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class EditBudgetCommandHandler : IRequestHandler<EditBudgetCommand, Result>
{
    private readonly IBudgetsRepository _budgetsRepository;
    private readonly PermissionService _permissionService;
    private readonly ValidationService _validationService;

    public EditBudgetCommandHandler(
        IBudgetsRepository budgetsRepository,
        PermissionService permissionService,
        ValidationService validationService)
    {
        _budgetsRepository = budgetsRepository;
        _validationService = validationService;
        _permissionService = permissionService;
    }

    public async Task<Result> Handle(EditBudgetCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyEditBudgetAction(command.UserId, command.BudgetId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult;
        }

        var (_, budget) = permissionResult.Value;

        var budgetValidationResult =
            _validationService.ValidateBudget(
                command.Amount,
                command.Currency,
                command.Description,
                command.Scope,
                command.Frequency,
                command.StartDate,
                command.EndDate,
                command.CommencementDay,
                command.TargetGroupIds);

        if (budgetValidationResult.IsFailure)
        {
            return budgetValidationResult;
        }

        var budgetActionPermissionResult = await _permissionService.VerifyBudgetAction(command.UserId, command.Scope, command.TargetGroupIds, ct);

        if (budgetActionPermissionResult.IsFailure)
        {
            return budgetActionPermissionResult;
        }

        var now = DateTime.UtcNow;
        var isActive = command.Activate ?? budget.IsActive;

        if (isActive && !budget.IsActive)
        {
            var deactivationResult = await _budgetsRepository.DeactivateAllByUserId(command.UserId, ct);
            if (deactivationResult.IsFailure)
            {
                return deactivationResult;
            }
        }

        var editedBudget = budget with
        {
            Amount = command.Amount,
            Currency = command.Currency,
            Description = command.Description,
            Frequency = command.Frequency,
            Scope = command.Scope,
            TargetGroupIds = command.TargetGroupIds,
            IsActive = isActive,
            CommencementDay = command.CommencementDay,
            StartDate = command.StartDate,
            EndDate = command.EndDate,
            Updated = now,
        };

        return await _budgetsRepository.Update(editedBudget, ct);
    }
}