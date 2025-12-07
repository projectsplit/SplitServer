using CSharpFunctionalExtensions;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Services;

public class PermissionService
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly ITransfersRepository _transfersRepository;

    public PermissionService(
        IGroupsRepository groupsRepository,
        IUsersRepository usersRepository,
        IExpensesRepository expensesRepository,
        ITransfersRepository transfersRepository)
    {
        _groupsRepository = groupsRepository;
        _usersRepository = usersRepository;
        _expensesRepository = expensesRepository;
        _transfersRepository = transfersRepository;
    }

    public async Task<Result<(User user, Group group, string memberId)>> VerifyGroupAction(
        string userId,
        string groupId,
        CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(userId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<(User user, Group group, string memberId)>($"User with id {userId} was not found");
        }

        var user = userMaybe.Value;

        var groupMaybe = await _groupsRepository.GetById(groupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure<(User user, Group group, string memberId)>($"Group with id {groupId} was not found");
        }

        var group = groupMaybe.Value;

        var memberId = group.Members.FirstOrDefault(m => m.UserId == userId)?.Id;

        if (memberId is null)
        {
            return Result.Failure<(User user, Group group, string memberId)>("User must be a group member");
        }

        return (user, group, memberId);
    }

    public async Task<Result<(User user, Group group, Expense expense, string memberId)>> VerifyExpenseAction(
        string userId,
        string expenseId,
        CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(userId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<(User user, Group group, Expense expense, string memberId)>($"User with id {userId} was not found");
        }

        var user = userMaybe.Value;

        var expenseMaybe = await _expensesRepository.GetById(expenseId, ct);

        if (expenseMaybe.HasNoValue)
        {
            return Result.Failure<(User user, Group group, Expense expense, string memberId)>($"Expense with id {expenseId} was not found");
        }

        var expense = expenseMaybe.Value;

        if (expense is not GroupExpense groupExpense)
        {
            return Result.Failure<(User user, Group group, Expense expense, string memberId)>($"Expense with id {expenseId} was not found");
        }

        var groupMaybe = await _groupsRepository.GetById(groupExpense.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure<(User user, Group group, Expense expense, string memberId)>($"Group with id {groupExpense.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        var memberId = group.Members.FirstOrDefault(m => m.UserId == userId)?.Id;

        if (memberId is null)
        {
            return Result.Failure<(User user, Group group, Expense expense, string memberId)>("User must be a group member");
        }

        return (user, group, expense, memberId);
    }

    public async Task<Result<(User user, Group group, Transfer transfer, string memberId)>> VerifyTransferAction(
        string userId,
        string transferId,
        CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(userId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<(User user, Group group, Transfer transfer, string memberId)>($"User with id {userId} was not found");
        }

        var user = userMaybe.Value;

        var transferMaybe = await _transfersRepository.GetById(transferId, ct);

        if (transferMaybe.HasNoValue)
        {
            return Result.Failure<(User user, Group group, Transfer transfer, string memberId)>(
                $"Transfer with id {transferId} was not found");
        }

        var transfer = transferMaybe.Value;

        var groupMaybe = await _groupsRepository.GetById(transfer.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure<(User user, Group group, Transfer transfer, string memberId)>(
                $"Group with id {transfer.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        var memberId = group.Members.FirstOrDefault(m => m.UserId == userId)?.Id;

        if (memberId is null)
        {
            return Result.Failure<(User user, Group group, Transfer transfer, string memberId)>("User must be a group member");
        }

        return (user, group, transfer, memberId);
    }
}