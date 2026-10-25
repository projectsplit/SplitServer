using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Commands;

public class CreatePersonalExpenseCommandHandler : IRequestHandler<CreatePersonalExpenseCommand, Result<CreateExpenseResponse>>
{
    private readonly IExpensesRepository _expensesRepository;
    private readonly ValidationService _validationService;
    private readonly UserLabelService _userLabelService;

    public CreatePersonalExpenseCommandHandler(
        IExpensesRepository expensesRepository,
        PermissionService permissionService,
        ValidationService validationService,
        UserLabelService userLabelService)
    {
        _expensesRepository = expensesRepository;
        _validationService = validationService;
        _userLabelService = userLabelService;
    }

    public async Task<Result<CreateExpenseResponse>> Handle(CreatePersonalExpenseCommand command, CancellationToken ct)
    {
        var expenseIdResult = ClientIds.Resolve(command.ExpenseId);

        if (expenseIdResult.IsFailure)
        {
            return expenseIdResult.ConvertFailure<CreateExpenseResponse>();
        }

        var expenseId = expenseIdResult.Value;

        if (command.ExpenseId is not null)
        {
            var isStoredResult =
                await ClientIds.IsAlreadyStored<Expense, PersonalExpense>(_expensesRepository, expenseId, command.UserId, ct);

            if (isStoredResult.IsFailure)
            {
                return isStoredResult.ConvertFailure<CreateExpenseResponse>();
            }

            if (isStoredResult.Value)
            {
                return new CreateExpenseResponse { ExpenseId = expenseId };
            }
        }

        var expenseValidationResult = _validationService.ValidatePersonalExpense(
            command.Amount,
            command.Currency);

        if (expenseValidationResult.IsFailure)
        {
            return Result.Failure<CreateExpenseResponse>(expenseValidationResult.Error);
        }

        var now = DateTime.UtcNow;

        var addLabelsToGroupResult = await _userLabelService.AddUserLabelsIfMissing(command.UserId, command.Labels, now, ct);

        if (addLabelsToGroupResult.IsFailure)
        {
            return addLabelsToGroupResult.ConvertFailure<CreateExpenseResponse>();
        }

        var personalExpense = new PersonalExpense
        {
            Id = expenseId,
            Created = now,
            Updated = now,
            CreatorId = command.UserId,
            Amount = command.Amount,
            Occurred = command.Occurred ?? now,
            Description = command.Description,
            Currency = command.Currency,
            Labels = command.Labels.Select(x => x.Text).ToList(),
            Location = command.Location,
            RecurringExpenseId = command.RecurringExpenseId
        };

        var writeResult = await _expensesRepository.Insert(personalExpense, ct);

        if (writeResult.IsFailure)
        {
            // Two sends of one expense can both get past the check above. The later insert then
            // fails on the duplicate id, and the expense it carries is already stored.
            if (command.ExpenseId is not null)
            {
                var isStoredResult =
                    await ClientIds.IsAlreadyStored<Expense, PersonalExpense>(_expensesRepository, expenseId, command.UserId, ct);

                if (isStoredResult.IsSuccess && isStoredResult.Value)
                {
                    return new CreateExpenseResponse { ExpenseId = expenseId };
                }
            }

            return writeResult.ConvertFailure<CreateExpenseResponse>();
        }

        return new CreateExpenseResponse
        {
            ExpenseId = expenseId
        };
    }
}