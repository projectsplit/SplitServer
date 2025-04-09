using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class LeaveGroupCommandHandler : IRequestHandler<LeaveGroupCommand, Result>
{
    private readonly PermissionService _permissionService;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly ITransfersRepository _transfersRepository;
    private readonly IUserActivityRepository _userActivityRepository;

    public LeaveGroupCommandHandler(
        PermissionService permissionService,
        IGroupsRepository groupsRepository,
        IExpensesRepository expensesRepository,
        ITransfersRepository transfersRepository,
        IUserActivityRepository userActivityRepository)
    {
        _permissionService = permissionService;
        _groupsRepository = groupsRepository;
        _expensesRepository = expensesRepository;
        _transfersRepository = transfersRepository;
        _userActivityRepository = userActivityRepository;
    }

    public async Task<Result> Handle(LeaveGroupCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyGroupAction(command.UserId, command.GroupId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult;
        }

        var (user, group, memberId) = permissionResult.Value;

        var memberToRemove = group.Members.First(m => m.Id == memberId);

        var memberHasAnyActivity =
            await _expensesRepository.ExistsInAnyExpense(command.GroupId, memberToRemove.Id, ct) ||
            await _transfersRepository.ExistsInAnyTransfer(command.GroupId, memberToRemove.Id, ct);

        var now = DateTime.UtcNow;

        var editedGroup = memberHasAnyActivity
            ? GroupWithReplacedMember(group, memberToRemove, user, now)
            : GroupWithRemovedMember(group, memberToRemove, now);

        var groupUpdateResult = await _groupsRepository.Update(editedGroup, ct);

        if (groupUpdateResult.IsFailure)
        {
            return groupUpdateResult;
        }

        return await _userActivityRepository.ClearRecentGroupForUser(command.UserId, command.GroupId, now, ct);
    }

    private static Group GroupWithReplacedMember(Group group, Member memberToRemove, User user, DateTime now)
    {
        var newGuest = new Guest
        {
            Id = memberToRemove.Id,
            Name = $"{user.Username}-guest",
            Joined = memberToRemove.Joined
        };

        return group with
        {
            Guests = group.Guests.Concat([newGuest]).ToList(),
            Members = group.Members.Where(x => x.Id != memberToRemove.Id).ToList(),
            Updated = now
        };
    }

    private static Group GroupWithRemovedMember(Group group, Member memberToRemove, DateTime now)
    {
        return group with
        {
            Members = group.Members.Where(x => x.Id != memberToRemove.Id).ToList(),
            Updated = now
        };
    }
}