using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class AddGuestCommandHandler : IRequestHandler<AddGuestCommand, Result>
{
    private readonly PermissionService _permissionService;
    private readonly IGroupsRepository _groupsRepository;

    public AddGuestCommandHandler(
        PermissionService permissionService,
        IGroupsRepository groupsRepository)
    {
        _permissionService = permissionService;
        _groupsRepository = groupsRepository;
    }

    public async Task<Result> Handle(AddGuestCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyGroupAction(command.UserId, command.GroupId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult;
        }

        var (_, group, _) = permissionResult.Value;

        if (group.Guests.Any(x => x.Name == command.GuestName))
        {
            return Result.Failure<Result>($"Guest with name {command.GuestName} already exists");
        }

        var now = DateTime.UtcNow;

        var newGuest = new Guest
        {
            Id = Guid.NewGuid().ToString(),
            Name = command.GuestName,
            Joined = now
        };

        var updatedGroup = group with
        {
            Guests = group.Guests.Concat([newGuest]).ToList(),
            Updated = now
        };

        var updateResult = await _groupsRepository.Update(updatedGroup, ct);

        if (updateResult.IsFailure)
        {
            return updateResult.ConvertFailure<Result>();
        }

        return Result.Success();
    }
}