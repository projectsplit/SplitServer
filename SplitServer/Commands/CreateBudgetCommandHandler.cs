using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Commands;

public class CreateBudgetCommandHandler : IRequestHandler<CreateBudgetCommand, Result<CreateBudgetResponse>>
{
    private readonly IBudgetsRepository _budgetsRepository;
    private readonly PermissionService _permissionService;
    private readonly ValidationService _validationService;

    public CreateBudgetCommandHandler(
        IBudgetsRepository budgetsRepository,
        PermissionService permissionService,
        ValidationService validationService)
    {
        _budgetsRepository = budgetsRepository;
        _validationService = validationService;
        _permissionService = permissionService;
    }

    public async Task<Result<CreateBudgetResponse>> Handle(CreateBudgetCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyBudgetAction(command.UserId, command.Scope, command.TargetGroupIds, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult.ConvertFailure<CreateBudgetResponse>();
        }

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
            return Result.Failure<CreateBudgetResponse>(budgetValidationResult.Error);
        }

        var now = DateTime.UtcNow;

        var userBudgets = await _budgetsRepository.GetAllByUserId(command.UserId, ct);
        var isFirstBudget = userBudgets.Count == 0;
        var isActive = isFirstBudget || (command.Activate ?? false);

        if (isActive && !isFirstBudget)
        {
            var deactivationResult = await _budgetsRepository.DeactivateAllByUserId(command.UserId, ct);
            if (deactivationResult.IsFailure)
            {
                return deactivationResult.ConvertFailure<CreateBudgetResponse>();
            }
        }

        var budgetId = Guid.NewGuid().ToString();

        var newBudget = new Budget
        {
            Id = budgetId,
            UserId = command.UserId,
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
            Created = now,
            Updated = now,
        };

        var writeResult = await _budgetsRepository.Insert(newBudget, ct);

        if (writeResult.IsFailure)
        {
            return writeResult.ConvertFailure<CreateBudgetResponse>();
        }

        return new CreateBudgetResponse
        {
            BudgetId = budgetId
        };
    }
}