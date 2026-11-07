using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class DeclineInvitationCommandHandler : IRequestHandler<DeclineInvitationCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IInvitationsRepository _invitationsRepository;
    private readonly IGroupsRepository _groupsRepository;

    public DeclineInvitationCommandHandler(
        IUsersRepository usersRepository,
        IInvitationsRepository invitationsRepository,
        IGroupsRepository groupsRepository)
    {
        _usersRepository = usersRepository;
        _invitationsRepository = invitationsRepository;
        _groupsRepository = groupsRepository;
    }

    public async Task<Result> Handle(DeclineInvitationCommand command, CancellationToken ct)
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

        if (invitation.ReceiverId != command.UserId)
        {
            return Result.Failure("You cannot decline this invitation");
        }

        var groupMaybe = await _groupsRepository.GetById(invitation.GroupId, ct);

        if (groupMaybe.HasValue && GuestMerge.IsUnfinished(invitation, groupMaybe.Value))
        {
            return Result.Failure(GuestMerge.UnfinishedError);
        }

        return await _invitationsRepository.Delete(command.InvitationId, ct);
    }
}