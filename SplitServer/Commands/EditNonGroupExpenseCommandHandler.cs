using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class EditNonGroupExpenseCommandHandler : IRequestHandler<EditNonGroupExpenseCommand, Result>
{
    private const string NonGroupExpensesUrl = "/shared/nongroup/expenses";

    private readonly IExpensesRepository _expensesRepository;
    private readonly IUsersRepository _usersRepository;
    private readonly PermissionService _permissionService;
    private readonly ValidationService _validationService;
    private readonly UserLabelService _userLabelService;
    private readonly NotificationService _notificationService;

    public EditNonGroupExpenseCommandHandler(
        IExpensesRepository expensesRepository,
        IUsersRepository usersRepository,
        PermissionService permissionService,
        ValidationService validationService,
        UserLabelService userLabelService,
        NotificationService notificationService)
    {
        _expensesRepository = expensesRepository;
        _usersRepository = usersRepository;
        _validationService = validationService;
        _permissionService = permissionService;
        _userLabelService = userLabelService;
        _notificationService = notificationService;
    }

    public async Task<Result> Handle(EditNonGroupExpenseCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyNonGroupExpenseAction(command.UserId, command.ExpenseId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult;
        }

        var (_, nonGroupExpense) = permissionResult.Value;

        var expenseValidationResult = _validationService.ValidateNonGroupExpense(
            command.Payments,
            command.Shares,
            command.Amount,
            command.Currency,
            command.UserId);

        if (expenseValidationResult.IsFailure)
        {
            return expenseValidationResult;
        }

        var now = DateTime.UtcNow;

        var addLabelsToUserResult = await _userLabelService.AddUserLabelsIfMissing(command.UserId, command.Labels, now, ct);

        if (addLabelsToUserResult.IsFailure)
        {
            return addLabelsToUserResult;
        }

        var editedExpense = nonGroupExpense with
        {
            Updated = now,
            Amount = command.Amount,
            Occurred = command.Occurred ?? now,
            Description = command.Description,
            Currency = command.Currency,
            Payments = command.Payments,
            Shares = command.Shares,
            Labels = command.Labels.Select(x => x.Text).ToList(),
            Location = command.Location
        };

        var updateResult = await _expensesRepository.Update(editedExpense, ct);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await NotifyUsersWithChangedAmounts(command, nonGroupExpense, ct);

        return updateResult;
    }

    /// <summary>
    /// Notifies only those whose own money moved, so renaming or relabelling an expense tells
    /// nobody. Someone removed is still told: their amount effectively went to zero, which changes
    /// what they are owed just as much as being added does.
    /// </summary>
    private async Task NotifyUsersWithChangedAmounts(
        EditNonGroupExpenseCommand command,
        NonGroupExpense originalExpense,
        CancellationToken ct)
    {
        var before = AmountChanges.Snapshot(
            originalExpense.Payments.Select(x => (x.UserId, x.Amount)),
            originalExpense.Shares.Select(x => (x.UserId, x.Amount)));

        var after = AmountChanges.Snapshot(
            command.Payments.Select(x => (x.UserId, x.Amount)),
            command.Shares.Select(x => (x.UserId, x.Amount)));

        // Re-denominating leaves every number untouched while changing what all of them mean, so
        // it counts as a change for everyone on the expense.
        var changedUserIds = originalExpense.Currency == command.Currency
            ? AmountChanges.GetChangedKeys(before, after)
            : before.Keys.Concat(after.Keys);

        var recipientUserIds = changedUserIds
            .Where(x => x != command.UserId)
            .Distinct()
            .ToList();

        if (recipientUserIds.Count == 0)
        {
            return;
        }

        var editorMaybe = await _usersRepository.GetById(command.UserId, ct);

        var editorUsername = editorMaybe.HasValue ? editorMaybe.Value.Username : "Someone";

        await _notificationService.Notify(
            recipientUserIds,
            "Expense edited",
            $"{editorUsername} edited \"{command.Description}\" ({command.Amount} {command.Currency})",
            NonGroupExpensesUrl,
            ct);
    }
}