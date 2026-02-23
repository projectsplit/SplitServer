using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Commands;

public class CreatePersonalExpenseCommandHandler: IRequestHandler<CreatePersonalExpenseCommand, Result<CreateExpenseResponse>>
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

        var expenseId = Guid.NewGuid().ToString();

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
            Location = command.Location
        };

        var writeResult = await _expensesRepository.Insert(personalExpense, ct);

        if (writeResult.IsFailure)
        {
            return writeResult.ConvertFailure<CreateExpenseResponse>();
        }

        return new CreateExpenseResponse
        {
            ExpenseId = expenseId
        };
    }
}