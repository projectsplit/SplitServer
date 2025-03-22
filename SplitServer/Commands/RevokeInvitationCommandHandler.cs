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

        return await _invitationsRepository.DeleteByGroupIdAndReceiverId(command.ReceiverId, command.GroupId, ct);
    }
}