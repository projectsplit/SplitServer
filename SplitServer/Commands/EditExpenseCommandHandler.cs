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
    private readonly NotificationService _notificationService;

    public EditExpenseCommandHandler(
        IExpensesRepository expensesRepository,
        PermissionService permissionService,
        ValidationService validationService,
        GroupService groupService,
        NotificationService notificationService)
    {
        _expensesRepository = expensesRepository;
        _validationService = validationService;
        _groupService = groupService;
        _permissionService = permissionService;
        _notificationService = notificationService;
    }

    public async Task<Result> Handle(EditExpenseCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyExpenseAction(command.UserId, command.ExpenseId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult;
        }

        var (user, group, expense, _) = permissionResult.Value;

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

        var updateResult = await _expensesRepository.Update(editedExpense, ct);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await NotifyMembersWithChangedAmounts(command, group, groupExpense, user.Username, ct);

        return updateResult;
    }

    /// <summary>
    /// Notifies only those whose own money moved, so renaming or relabelling an expense tells
    /// nobody. Someone removed is still told: their amount effectively went to zero, which changes
    /// what they are owed just as much as being added does.
    /// </summary>
    private async Task NotifyMembersWithChangedAmounts(
        EditExpenseCommand command,
        Group group,
        GroupExpense originalExpense,
        string editorUsername,
        CancellationToken ct)
    {
        var before = AmountChanges.Snapshot(
            originalExpense.Payments.Select(x => (x.MemberId, x.Amount)),
            originalExpense.Shares.Select(x => (x.MemberId, x.Amount)));

        var after = AmountChanges.Snapshot(
            command.Payments.Select(x => (x.MemberId, x.Amount)),
            command.Shares.Select(x => (x.MemberId, x.Amount)));

        // Re-denominating leaves every number untouched while changing what all of them mean, so
        // it counts as a change for everyone on the expense.
        var changedMemberIds = originalExpense.Currency == command.Currency
            ? AmountChanges.GetChangedKeys(before, after)
            : before.Keys.Concat(after.Keys);

        var recipientUserIds = GroupService.GetInvolvedUserIdsToNotify(
            group,
            changedMemberIds,
            command.UserId);

        await _notificationService.Notify(
            recipientUserIds,
            group.Name,
            $"{editorUsername} edited \"{command.Description}\" ({command.Amount} {command.Currency})",
            $"/shared/{group.Id}/expenses",
            ct);
    }
}