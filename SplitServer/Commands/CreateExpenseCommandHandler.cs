using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Commands;

public class CreateExpenseCommandHandler : IRequestHandler<CreateExpenseCommand, Result<CreateExpenseResponse>>
{
    private readonly IExpensesRepository _expensesRepository;
    private readonly PermissionService _permissionService;
    private readonly ValidationService _validationService;

    public CreateExpenseCommandHandler(
        IExpensesRepository expensesRepository,
        PermissionService permissionService,
        ValidationService validationService)
    {
        _expensesRepository = expensesRepository;
        _validationService = validationService;
        _permissionService = permissionService;
    }

    public async Task<Result<CreateExpenseResponse>> Handle(CreateExpenseCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyGroupAction(command.UserId, command.GroupId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult.ConvertFailure<CreateExpenseResponse>();
        }

        var (_, group, memberId) = permissionResult.Value;

        var expenseValidationResult =
            _validationService.ValidateExpense(group, command.Payments, command.Shares, command.Amount, command.Currency);

        if (expenseValidationResult.IsFailure)
        {
            return Result.Failure<CreateExpenseResponse>(expenseValidationResult.Error);
        }

        var now = DateTime.UtcNow;
        var expenseId = Guid.NewGuid().ToString();

        var newExpense = new Expense
        {
            Id = expenseId,
            IsDeleted = false,
            Created = now,
            Updated = now,
            GroupId = command.GroupId,
            CreatorId = memberId,
            Amount = command.Amount,
            Occurred = command.Occurred ?? now,
            Description = command.Description,
            Currency = command.Currency,
            Payments = command.Payments,
            Shares = command.Shares,
            Labels = command.Labels,
            Location = command.Location
        };

        var writeResult = await _expensesRepository.Insert(newExpense, ct);

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