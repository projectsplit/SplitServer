using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class RevokeInvitationCommandHandler : IRequestHandler<RevokeInvitationCommand, Result>
{
    private readonly PermissionService _permissionService;
    private readonly IInvitationsRepository _invitationsRepository;

    public RevokeInvitationCommandHandler(
        IInvitationsRepository invitationsRepository,
        PermissionService permissionService)
    {
        _invitationsRepository = invitationsRepository;
        _permissionService = permissionService;
    }

    public async Task<Result> Handle(RevokeInvitationCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyGroupAction(command.UserId, command.GroupId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult;
        }

        var (_, group, _) = permissionResult.Value;

        var invitationMaybe = await _invitationsRepository.GetByGroupIdAndReceiverId(command.ReceiverId, command.GroupId, ct);

        if (invitationMaybe.HasValue && GuestMerge.IsUnfinished(invitationMaybe.Value, group))
        {
            return Result.Failure(GuestMerge.UnfinishedError);
        }

        return await _invitationsRepository.DeleteByGroupIdAndReceiverId(command.ReceiverId, command.GroupId, ct);
    }
}