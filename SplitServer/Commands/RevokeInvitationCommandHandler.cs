using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;

namespace SplitServer.Commands;

public class RevokeInvitationCommandHandler : IRequestHandler<RevokeInvitationCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IInvitationsRepository _invitationsRepository;
    private readonly IGroupsRepository _groupsRepository;

    public RevokeInvitationCommandHandler(
        IUsersRepository usersRepository,
        IInvitationsRepository invitationsRepository,
        IGroupsRepository groupsRepository)
    {
        _usersRepository = usersRepository;
        _invitationsRepository = invitationsRepository;
        _groupsRepository = groupsRepository;
    }

    public async Task<Result> Handle(RevokeInvitationCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }

        var invitationMaybe = await _invitationsRepository.GetById(command.InvitationId, ct);

        if (invitationMaybe.HasNoValue)
        {
            return Result.Failure($"Invitation with id {command.InvitationId} was not found");
        }

        var invitation = invitationMaybe.Value;
        
        var groupMaybe = await _groupsRepository.GetById(invitation.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure($"Group with id {invitation.GroupId} was not found");
        }
        
        var group = groupMaybe.Value;

        if (group.Members.All(x => x.UserId != command.UserId))
        {
            return Result.Failure("You must be a group member to revoke invitations");
        }

        var deleteInvitationResult = await _invitationsRepository.Delete(command.InvitationId, ct);

        if (deleteInvitationResult.IsFailure)
        {
            return deleteInvitationResult.ConvertFailure<Result>();
        }

        return Result.Success();
    }
}