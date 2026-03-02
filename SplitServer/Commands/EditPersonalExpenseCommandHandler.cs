using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class EditPersonalExpenseCommandHandler : IRequestHandler<EditPersonalExpenseCommand, Result>
{
    private readonly IExpensesRepository _expensesRepository;
    private readonly PermissionService _permissionService;
    private readonly ValidationService _validationService;
    private readonly UserLabelService _userLabelService;

    public EditPersonalExpenseCommandHandler(
        IExpensesRepository expensesRepository,
        PermissionService permissionService,
        ValidationService validationService,
        UserLabelService userLabelService)
    {
        _expensesRepository = expensesRepository;
        _validationService = validationService;
        _permissionService = permissionService;
        _userLabelService = userLabelService;
    }

    public async Task<Result> Handle(EditPersonalExpenseCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyPersonalExpenseAction(command.UserId, command.ExpenseId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult;
        }

        var (_, personalExpense) = permissionResult.Value;

        var expenseValidationResult = _validationService.ValidatePersonalExpense(
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

        var editedExpense = personalExpense with
        {
            Updated = now,
            Amount = command.Amount,
            Occurred = command.Occurred ?? now,
            Description = command.Description,
            Currency = command.Currency,
            Labels = command.Labels.Select(x => x.Text).ToList(),
            Location = command.Location
        };

        return await _expensesRepository.Update(editedExpense, ct);
    }
}