using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Commands;

public class CreateExpenseCommandHandler : IRequestHandler<CreateExpenseCommand, Result<CreateExpenseResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IExpensesRepository _expensesRepository;

    public CreateExpenseCommandHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        IExpensesRepository expensesRepository)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _expensesRepository = expensesRepository;
    }

    public async Task<Result<CreateExpenseResponse>> Handle(CreateExpenseCommand command, CancellationToken ct)
    {
        if (command.Amount <= 0)
        {
            return Result.Failure<CreateExpenseResponse>("Amount must be greater than 0");
        }

        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<CreateExpenseResponse>($"User with id {command.UserId} was not found");
        }

        var groupMaybe = await _groupsRepository.GetById(command.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure<CreateExpenseResponse>($"Group with id {command.GroupId} was not found");
        }

        var group = groupMaybe.Value;
        var creatorMemberId = group.Members.FirstOrDefault(m => m.UserId == command.UserId)?.Id;

        if (creatorMemberId is null)
        {
            return Result.Failure<CreateExpenseResponse>("User must be a group member");
        }

        var payers = command.Payments.Select(x => x.MemberId).ToList();
        var participants = command.Shares.Select(x => x.MemberId).ToList();

        if (payers.GroupBy(x => x).Any(g => g.Count() > 1) || participants.GroupBy(x => x).Any(g => g.Count() > 1))
        {
            return Result.Failure<CreateExpenseResponse>("Duplicate members not allowed");
        }

        var members = group.Members.Select(x => x.Id).ToList();
        var guests = group.Guests.Select(x => x.Id).ToList();

        if (payers.Concat(participants).Any(x => !members.Concat(guests).Contains(x)))
        {
            return Result.Failure<CreateExpenseResponse>("Payers and participants must be group members or guests");
        }

        if (command.Shares.Any(x => x.Amount <= 0))
        {
            return Result.Failure<CreateExpenseResponse>("Every share amount must be greater than 0");
        }

        if (command.Payments.Any(x => x.Amount <= 0))
        {
            return Result.Failure<CreateExpenseResponse>("Every payment amount must be greater than 0");
        }

        var shareAmount = command.Shares.Sum(x => x.Amount);
        var paymentAmount = command.Payments.Sum(x => x.Amount);

        if (shareAmount != command.Amount)
        {
            return Result.Failure<CreateExpenseResponse>("Share amount sum must be equal to expense amount");
        }

        if (paymentAmount != command.Amount)
        {
            return Result.Failure<CreateExpenseResponse>("Payment amount sum must be equal to expense amount");
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
            CreatorId = creatorMemberId,
            Amount = command.Amount,
            Occured = command.Occured ?? now,
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