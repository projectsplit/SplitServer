using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

public class EditNonGroupExpenseCommandHandler : IRequestHandler<EditNonGroupExpenseCommand, Result>
{
    private readonly IExpensesRepository _expensesRepository;
    private readonly PermissionService _permissionService;
    private readonly ValidationService _validationService;
    private readonly GroupService _groupService;
    private readonly UserLabelService _userLabelService;
    
    public EditNonGroupExpenseCommandHandler(
        IExpensesRepository expensesRepository,
        PermissionService permissionService,
        ValidationService validationService,
        GroupService groupService,
        UserLabelService userLabelService)
    {
        _expensesRepository = expensesRepository;
        _validationService = validationService;
        _groupService = groupService;
        _permissionService = permissionService;
        _userLabelService = userLabelService;
    }

    public async Task<Result> Handle(EditNonGroupExpenseCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyNonGroupExpenseAction(command.UserId, command.ExpenseId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult;
        }

        var (_,expense) = permissionResult.Value;

        if (expense is not NonGroupExpense nonGroupExpense)
        {
            return Result.Failure($"Expense with id {expense.Id} was not found");
        }

        var expenseValidationResult = _validationService.ValidateNonGroupExpense(
            command.Payments,
            command.Shares,
            command.Amount,
            command.Currency);

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

        return await _expensesRepository.Update(editedExpense, ct);
    }
}