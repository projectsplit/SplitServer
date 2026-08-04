using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class DeleteNonGroupExpenseCommandHandler : IRequestHandler<DeleteNonGroupExpenseCommand, Result>
{
    private const string NonGroupExpensesUrl = "/shared/nongroup/expenses";

    private readonly PermissionService _permissionService;
    private readonly IExpensesRepository _expensesRepository;
    private readonly NotificationService _notificationService;

    public DeleteNonGroupExpenseCommandHandler(
        IExpensesRepository expensesRepository,
        PermissionService permissionService,
        NotificationService notificationService)
    {
        _expensesRepository = expensesRepository;
        _permissionService = permissionService;
        _notificationService = notificationService;
    }

    public async Task<Result> Handle(DeleteNonGroupExpenseCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyNonGroupExpenseAction(command.UserId, command.ExpenseId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult;
        }

        var (user, nonGroupExpense) = permissionResult.Value;

        var deleteResult = await _expensesRepository.Delete(command.ExpenseId, ct);

        if (deleteResult.IsFailure)
        {
            return deleteResult;
        }

        // Everyone who was on it: a deletion zeroes their amount, which moves their balance just as
        // surely as an edit would, and the expense is gone from the list with nothing to explain it.
        var recipientUserIds = nonGroupExpense.Payments.Select(x => x.UserId)
            .Concat(nonGroupExpense.Shares.Select(x => x.UserId))
            .Where(x => x != command.UserId)
            .Distinct()
            .ToList();

        await _notificationService.Notify(
            recipientUserIds,
            "Expense deleted",
            $"{user.Username} deleted \"{nonGroupExpense.Description}\" ({nonGroupExpense.Amount} {nonGroupExpense.Currency})",
            NonGroupExpensesUrl,
            ct);

        return deleteResult;
    }
}