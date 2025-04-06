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

    public RemoveGroupMemberCommandHandler(
        PermissionService permissionService,
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository)
    {
        _permissionService = permissionService;
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
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

        var userToRemoveMaybe = await _usersRepository.GetById(memberToRemove.UserId, ct);

        var userToRemove = userToRemoveMaybe.GetValueOrDefault();

        var newGuest = new Guest
        {
            Id = memberToRemove.Id,
            Name = userToRemove?.Username is not null ? $"{userToRemove.Username}-guest" : $"{memberToRemove.Id.Take(8)}-guest",
            Joined = memberToRemove.Joined
        };

        var editedGroup = group with
        {
            Guests = group.Guests.Concat([newGuest]).ToList(),
            Members = group.Members.Where(x => x.Id != command.MemberId).ToList(),
            Updated = DateTime.UtcNow
        };

        return await _groupsRepository.Update(editedGroup, ct);
    }
}