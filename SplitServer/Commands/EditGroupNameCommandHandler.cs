using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class EditGroupNameCommandHandler : IRequestHandler<EditGroupNameCommand, Result>
{
    private readonly PermissionService _permissionService;
    private readonly IGroupsRepository _groupsRepository;

    public EditGroupNameCommandHandler(
        PermissionService permissionService,
        IGroupsRepository groupsRepository)
    {
        _permissionService = permissionService;
        _groupsRepository = groupsRepository;
    }

    public async Task<Result> Handle(EditGroupNameCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyGroupAction(command.UserId, command.GroupId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult;
        }

        var (_, group, _) = permissionResult.Value;

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return Result.Failure("Group name cannot be null or empty");
        }

        var updatedGroup = group with
        {
            Name = command.Name,
            Updated = DateTime.UtcNow
        };

        return await _groupsRepository.Update(updatedGroup, ct);
    }
}