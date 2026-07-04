using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class EditGroupNameCommandHandler : IRequestHandler<EditGroupNameCommand, Result>
{
    private readonly PermissionService _permissionService;
    private readonly IGroupsRepository _groupsRepository;
    private readonly PushNotificationService _pushNotificationService;

    public EditGroupNameCommandHandler(
        PermissionService permissionService,
        IGroupsRepository groupsRepository,
        PushNotificationService pushNotificationService)
    {
        _permissionService = permissionService;
        _groupsRepository = groupsRepository;
        _pushNotificationService = pushNotificationService;
    }

    public async Task<Result> Handle(EditGroupNameCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyGroupAction(command.UserId, command.GroupId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult;
        }

        var (user, group, _) = permissionResult.Value;

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return Result.Failure("Group name cannot be null or empty");
        }

        var updatedGroup = group with
        {
            Name = command.Name,
            Updated = DateTime.UtcNow
        };

        var updateResult = await _groupsRepository.Update(updatedGroup, ct);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        if (group.Name != command.Name)
        {
            var memberUserIds = group.Members
                .Where(m => m.UserId != command.UserId)
                .Select(m => m.UserId);

            _pushNotificationService.NotifyInBackground(
                memberUserIds,
                "Group renamed",
                $"{user.Username} renamed \"{group.Name}\" to \"{command.Name}\".",
                $"/shared/{command.GroupId}/expenses");
        }

        return Result.Success();
    }
}
