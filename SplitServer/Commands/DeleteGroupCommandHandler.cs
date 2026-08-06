using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;

namespace SplitServer.Commands;

public class DeleteGroupCommandHandler : IRequestHandler<DeleteGroupCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly IRecurringExpensesRepository _recurringExpensesRepository;
    private readonly ITransfersRepository _transfersRepository;
    private readonly IInvitationsRepository _invitationsRepository;
    private readonly IUserActivityRepository _userActivityRepository;

    public DeleteGroupCommandHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        IExpensesRepository expensesRepository,
        IRecurringExpensesRepository recurringExpensesRepository,
        ITransfersRepository transfersRepository,
        IInvitationsRepository invitationsRepository,
        IUserActivityRepository userActivityRepository)
    {
        _recurringExpensesRepository = recurringExpensesRepository;
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _expensesRepository = expensesRepository;
        _transfersRepository = transfersRepository;
        _invitationsRepository = invitationsRepository;
        _userActivityRepository = userActivityRepository;
    }

    public async Task<Result> Handle(DeleteGroupCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }

        var user = userMaybe.Value;

        var groupMaybe = await _groupsRepository.GetById(command.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure($"Group with id {command.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        if (group.OwnerId != user.Id)
        {
            return Result.Failure("This group does not belong to user");
        }

        var deleteGroupResult = await _groupsRepository.Delete(group.Id, ct);

        if (deleteGroupResult.IsFailure)
        {
            return deleteGroupResult;
        }

        var deleteExpensesResult = await _expensesRepository.DeleteByGroupId(group.Id, ct);

        if (deleteExpensesResult.IsFailure)
        {
            return deleteExpensesResult;
        }

        // Templates pointing at a group that no longer exists could only ever fail, so they go with it.
        var deleteRecurringExpensesResult = await _recurringExpensesRepository.DeleteByGroupId(group.Id, ct);

        if (deleteRecurringExpensesResult.IsFailure)
        {
            return deleteRecurringExpensesResult;
        }

        var deleteTransfersResult = await _transfersRepository.DeleteByGroupId(group.Id, ct);

        if (deleteTransfersResult.IsFailure)
        {
            return deleteTransfersResult;
        }

        var deleteInvitationsResult = await _invitationsRepository.DeleteByGroupId(group.Id, ct);

        if (deleteInvitationsResult.IsFailure)
        {
            return deleteInvitationsResult;
        }

        return await _userActivityRepository.ClearRecentGroupForAllUsers(group.Id, DateTime.UtcNow, ct);
    }
}