using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class EditGroupArchiveStatusCommandHandler : IRequestHandler<EditGroupArchiveStatusCommand, Result>
{
    private readonly PermissionService _permissionService;
    private readonly IGroupsRepository _groupsRepository;

    public EditGroupArchiveStatusCommandHandler(
        PermissionService permissionService,
        IGroupsRepository groupsRepository)
    {
        _permissionService = permissionService;
        _groupsRepository = groupsRepository;
    }

    public async Task<Result> Handle(EditGroupArchiveStatusCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyGroupAction(command.UserId, command.GroupId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult;
        }

        var (_, group, _) = permissionResult.Value;

        var updatedGroup = group with
        {
            IsArchived = command.IsArchived,
            Updated = DateTime.UtcNow
        };

        return await _groupsRepository.Update(updatedGroup, ct);
    }
}