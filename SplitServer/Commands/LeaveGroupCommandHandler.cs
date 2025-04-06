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

    public LeaveGroupCommandHandler(
        PermissionService permissionService,
        IGroupsRepository groupsRepository,
        IExpensesRepository expensesRepository,
        ITransfersRepository transfersRepository)
    {
        _permissionService = permissionService;
        _groupsRepository = groupsRepository;
        _expensesRepository = expensesRepository;
        _transfersRepository = transfersRepository;
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

        var editedGroup = memberHasAnyActivity
            ? GroupWithReplacedMember(group, memberToRemove, user)
            : GroupWithRemovedMember(group, memberToRemove);

        return await _groupsRepository.Update(editedGroup, ct);
    }

    private static Group GroupWithReplacedMember(Group group, Member memberToRemove, User user)
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