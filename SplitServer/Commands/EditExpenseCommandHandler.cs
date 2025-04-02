using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class EditExpenseCommandHandler : IRequestHandler<EditExpenseCommand, Result>
{
    private readonly IExpensesRepository _expensesRepository;
    private readonly PermissionService _permissionService;
    private readonly ValidationService _validationService;

    public EditExpenseCommandHandler(
        IExpensesRepository expensesRepository,
        PermissionService permissionService,
        ValidationService validationService)
    {
        _expensesRepository = expensesRepository;
        _validationService = validationService;
        _permissionService = permissionService;
    }

    public async Task<Result> Handle(EditExpenseCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyExpenseAction(command.UserId, command.ExpenseId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult;
        }

        var (_, group, expense, _) = permissionResult.Value;

        var expenseValidationResult =
            _validationService.ValidateExpense(group, command.Payments, command.Shares, command.Amount, command.Currency);

        if (expenseValidationResult.IsFailure)
        {
            return expenseValidationResult;
        }

        var now = DateTime.UtcNow;

        var editedExpense = expense with
        {
            Updated = now,
            Amount = command.Amount,
            Occurred = command.Occurred ?? now,
            Description = command.Description,
            Currency = command.Currency,
            Payments = command.Payments,
            Shares = command.Shares,
            Labels = command.Labels,
            Location = command.Location
        };

        return await _expensesRepository.Update(editedExpense, ct);
    }
}