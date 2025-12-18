using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class EditExpenseCommandHandler : IRequestHandler<EditExpenseCommand, Result>
{
    private readonly IExpensesRepository _expensesRepository;
    private readonly PermissionService _permissionService;
    private readonly ValidationService _validationService;
    private readonly GroupService _groupService;

    public EditExpenseCommandHandler(
        IExpensesRepository expensesRepository,
        PermissionService permissionService,
        ValidationService validationService,
        GroupService groupService)
    {
        _expensesRepository = expensesRepository;
        _validationService = validationService;
        _groupService = groupService;
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

        if (expense is not GroupExpense groupExpense)
        {
            return Result.Failure($"Expense with id {expense.Id} was not found");
        }

        var expenseValidationResult =
            _validationService.ValidateExpense(group, command.Payments, command.Shares, command.Amount, command.Currency);

        if (expenseValidationResult.IsFailure)
        {
            return expenseValidationResult;
        }

        var now = DateTime.UtcNow;

        var labelsWithIds = GroupService.CreateLabelsWithIds(command.Labels, group.Labels);

        var addLabelsToGroupResult = await _groupService.AddLabelsToGroupIfMissing(group, labelsWithIds, now, ct);

        if (addLabelsToGroupResult.IsFailure)
        {
            return addLabelsToGroupResult;
        }

        var editedExpense = groupExpense with
        {
            Updated = now,
            Amount = command.Amount,
            Occurred = command.Occurred ?? now,
            Description = command.Description,
            Currency = command.Currency,
            Payments = command.Payments,
            Shares = command.Shares,
            Labels = labelsWithIds.Select(x => x.Id).ToList(),
            Location = command.Location
        };

        return await _expensesRepository.Update(editedExpense, ct);
    }
}