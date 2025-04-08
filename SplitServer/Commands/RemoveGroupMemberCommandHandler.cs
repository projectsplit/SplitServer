using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class RemoveGroupMemberCommandHandler : IRequestHandler<RemoveGroupMemberCommand, Result>
{
    private readonly PermissionService _permissionService;
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly ITransfersRepository _transfersRepository;
    private readonly IUserActivityRepository _userActivityRepository;

    public RemoveGroupMemberCommandHandler(
        PermissionService permissionService,
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        IExpensesRepository expensesRepository,
        ITransfersRepository transfersRepository,
        IUserActivityRepository userActivityRepository)
    {
        _permissionService = permissionService;
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _expensesRepository = expensesRepository;
        _transfersRepository = transfersRepository;
        _userActivityRepository = userActivityRepository;
    }

    public async Task<Result> Handle(RemoveGroupMemberCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyGroupAction(command.UserId, command.GroupId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult;
        }

        var (_, group, memberId) = permissionResult.Value;

        if (memberId == command.MemberId)
        {
            return Result.Failure<Result>("You cannot remove yourself");
        }

        var memberToRemove = group.Members.FirstOrDefault(m => m.Id == command.MemberId);

        if (memberToRemove is null)
        {
            return Result.Failure<Result>("This member does not exist in this group");
        }

        var memberHasAnyActivity =
            await _expensesRepository.ExistsInAnyExpense(command.GroupId, memberToRemove.Id, ct) ||
            await _transfersRepository.ExistsInAnyTransfer(command.GroupId, memberToRemove.Id, ct);

        var editedGroup = memberHasAnyActivity
            ? await GroupWithReplacedMember(group, memberToRemove, ct)
            : GroupWithRemovedMember(group, memberToRemove);

        var groupUpdateResult = await _groupsRepository.Update(editedGroup, ct);

        if (groupUpdateResult.IsFailure)
        {
            return groupUpdateResult;
        }

        return await _userActivityRepository.ClearRecentGroupForUser(command.UserId, command.GroupId, ct);
    }

    private async Task<Group> GroupWithReplacedMember(Group group, Member memberToRemove, CancellationToken ct)
    {
        var userToRemoveMaybe = await _usersRepository.GetById(memberToRemove.UserId, ct);

        var userToRemove = userToRemoveMaybe.GetValueOrDefault();

        var newGuest = new Guest
        {
            Id = memberToRemove.Id,
            Name = userToRemove?.Username is not null ? $"{userToRemove.Username}-guest" : $"{memberToRemove.Id.Take(8)}-guest",
            Joined = memberToRemove.Joined
        };

        return group with
        {
            Guests = group.Guests.Concat([newGuest]).ToList(),
            Members = group.Members.Where(x => x.Id != memberToRemove.Id).ToList(),
            Updated = DateTime.UtcNow
        };
    }

    private static Group GroupWithRemovedMember(Group group, Member memberToRemove)
    {
        return group with
        {
            Members = group.Members.Where(x => x.Id != memberToRemove.Id).ToList(),
            Updated = DateTime.UtcNow
        };
    }
}