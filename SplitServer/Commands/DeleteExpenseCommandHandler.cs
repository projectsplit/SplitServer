using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class DeleteExpenseCommandHandler : IRequestHandler<DeleteExpenseCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly NotificationService _notificationService;

    public DeleteExpenseCommandHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        IExpensesRepository expensesRepository,
        NotificationService notificationService)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _expensesRepository = expensesRepository;
        _notificationService = notificationService;
    }

    public async Task<Result> Handle(DeleteExpenseCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }

        var expenseMaybe = await _expensesRepository.GetById(command.ExpenseId, ct);

        if (expenseMaybe.HasNoValue)
        {
            return Result.Failure($"Expense with id {command.ExpenseId} was not found");
        }

        var expense = expenseMaybe.Value;

        if (expense is not GroupExpense groupExpense)
        {
            return Result.Failure($"Expense with id {command.ExpenseId} was not found");
        }

        var groupMaybe = await _groupsRepository.GetById(groupExpense.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure($"Group with id {groupExpense.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        if (group.Members.All(x => x.UserId != command.UserId))
        {
            return Result.Failure("User must be a group member");
        }

        var deleteResult = await _expensesRepository.Delete(command.ExpenseId, ct);

        if (deleteResult.IsFailure)
        {
            return deleteResult;
        }

        // Everyone who was on it: a deletion zeroes their amount, which moves their balance just as
        // surely as an edit would, and the expense is gone from the list with nothing to explain it.
        var recipientUserIds = GroupService.GetInvolvedUserIdsToNotify(
            group,
            groupExpense.Payments.Select(x => x.MemberId)
                .Concat(groupExpense.Shares.Select(x => x.MemberId)),
            command.UserId);

        await _notificationService.Notify(
            recipientUserIds,
            group.Name,
            $"{userMaybe.Value.Username} deleted \"{groupExpense.Description}\" ({groupExpense.Amount} {groupExpense.Currency})",
            $"/shared/{group.Id}/expenses",
            ct);

        return deleteResult;
    }
}