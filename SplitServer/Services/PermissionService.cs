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
            return Result.Failure<(User user, Group group, Expense expense, string memberId)>(
                $"Group with id {groupExpense.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        var memberId = group.Members.FirstOrDefault(m => m.UserId == userId)?.Id;

        if (memberId is null)
        {
            return Result.Failure<(User user, Group group, Expense expense, string memberId)>("User must be a group member");
        }

        return (user, group, expense, memberId);
    }

    public async Task<Result<(User user, List<string>? targetGroupIds)>> VerifyBudgetAction(
        string userId,
        BudgetScope scope,
        List<string>? targetGroupIds,
        CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(userId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<(User user, List<string>? targetGroupIds)>($"User with id {userId} was not found");
        }

        var user = userMaybe.Value;

        var groups = await _groupsRepository.GetAllByUserId(userId, ct);
        var groupIds = groups.Select(g => g.Id).ToList();

        if (scope == BudgetScope.Group && groupIds is { Count: 0 })
        {
            return Result.Failure<(User user, List<string>? targetGroupIds)>(
                "User must belong to at least one group to create a budget for specific groups");
        }

        if (targetGroupIds is not { Count: > 0 })
        {
            return (user, targetGroupIds);
        }

        if (targetGroupIds.Count != targetGroupIds.Distinct().Count())
        {
            return Result.Failure<(User user, List<string>? targetGroupIds)>("Duplicate Group IDs are not allowed");
        }

        foreach (var targetId in targetGroupIds)
        {
            if (!groupIds.Contains(targetId))
            {
                return Result.Failure<(User user, List<string>? targetGroupIds)>(
                    $"Group ID '{targetId}' does not belong to the user or was not found");
            }
        }

        return (user, targetGroupIds);
    }

    public async Task<Result<(User user, NonGroupExpense expense)>> VerifyNonGroupExpenseAction(
        string userId,
        string expenseId,
        CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(userId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<(User user, NonGroupExpense expense)>($"User with id {userId} was not found");
        }

        var user = userMaybe.Value;

        var expenseMaybe = await _expensesRepository.GetById(expenseId, ct);

        if (expenseMaybe.HasNoValue)
        {
            return Result.Failure<(User user, NonGroupExpense expense)>($"Expense with id {expenseId} was not found");
        }

        var expense = expenseMaybe.Value;

        if (expense is not NonGroupExpense nonGroupExpense)
        {
            return Result.Failure<(User user, NonGroupExpense expense)>($"Expense with id {expenseId} was not found");
        }

        var userFoundInExpense = nonGroupExpense.Payments.Any(p => p.UserId == userId) ||
                                 nonGroupExpense.Shares.Any(s => s.UserId == userId);

        if (!userFoundInExpense)
        {
            return Result.Failure<(User user, NonGroupExpense expense)>(
                $"User with id {userId} was not found in expense with id {expenseId}");
        }

        return (user, nonGroupExpense);
    }

    public async Task<Result<(User user, Expense expense)>> VerifyPersonalExpenseAction(
        string userId,
        string expenseId,
        CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(userId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<(User user, Expense expense)>($"User with id {userId} was not found");
        }

        var user = userMaybe.Value;

        var expenseMaybe = await _expensesRepository.GetById(expenseId, ct);

        if (expenseMaybe.HasNoValue)
        {
            return Result.Failure<(User user, Expense expense)>($"Expense with id {expenseId} was not found");
        }

        var expense = expenseMaybe.Value;

        if (expense is not PersonalExpense personalExpense)
        {
            return Result.Failure<(User user, Expense expense)>($"Expense with id {expenseId} was not found");
        }

        var userFoundInExpense = personalExpense.CreatorId == userId;

        if (!userFoundInExpense)
        {
            return Result.Failure<(User user, Expense expense)>($"User with id {userId} was not found in expense with id {expenseId}");
        }

        return (user, personalExpense);
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

        if (transfer is not GroupTransfer groupTransfer)
        {
            return Result.Failure<(User user, Group group, Transfer transfer, string memberId)>(
                $"Expense with id {transferId} was not found");
        }

        var groupMaybe = await _groupsRepository.GetById(groupTransfer.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure<(User user, Group group, Transfer transfer, string memberId)>(
                $"Group with id {groupTransfer.GroupId} was not found");
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