using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Commands;

public class CreateNonGroupExpenseCommandHandler : IRequestHandler<CreateNonGroupExpenseCommand, Result<CreateExpenseResponse>>
{
    private readonly IExpensesRepository _expensesRepository;
    private readonly ValidationService _validationService;
    private readonly NonGroupService _nonGroupService;
    private readonly IUsersRepository _usersRepository;

    public CreateNonGroupExpenseCommandHandler(
        IExpensesRepository expensesRepository,
        PermissionService permissionService,
        ValidationService validationService,
        NonGroupService nonGroupService,
        IUsersRepository usersRepository)
    {
        _expensesRepository = expensesRepository;
        _validationService = validationService;
        _nonGroupService = nonGroupService;

        _usersRepository = usersRepository;
    }

    public async Task<Result<CreateExpenseResponse>> Handle(CreateNonGroupExpenseCommand command, CancellationToken ct)
    {

        var expenseValidationResult =
            _validationService.ValidateNonGroupExpense(command.Payments, command.Shares, command.Amount, command.Currency);

        if (expenseValidationResult.IsFailure)
        {
            return Result.Failure<CreateExpenseResponse>(expenseValidationResult.Error);
        }

        var now = DateTime.UtcNow;
        var user = await _usersRepository.GetById(command.UserId, ct);
        if (user.HasNoValue)
        {
            return Result.Failure<CreateExpenseResponse>("User not found");
        }
        var userResult = user.Value;

        var labelsWithIds = NonGroupService.CreateLabelsWithIds(command.Labels, userResult.Labels);

        var addLabelsToGroupResult = await _nonGroupService.AddLabelsToUserIfMissing(userResult, labelsWithIds, now, ct);

        if (addLabelsToGroupResult.IsFailure)
        {
            return addLabelsToGroupResult.ConvertFailure<CreateExpenseResponse>();
        }

        var expenseId = Guid.NewGuid().ToString();

        var newNonGroupExpense = new NonGroupExpense
        {
            Id = expenseId,
            Created = now,
            Updated = now,
            CreatorId = command.UserId,
            Amount = command.Amount,
            Occurred = command.Occurred ?? now,
            Description = command.Description,
            Currency = command.Currency,
            Payments = command.Payments,
            Shares = command.Shares,
            Labels = labelsWithIds.Select(x => x.Id).ToList(),
            Location = command.Location
        };

        var writeResult = await _expensesRepository.Insert(newNonGroupExpense, ct);

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