using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Commands;

public class AcceptInvitationCommandHandler : IRequestHandler<AcceptInvitationCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IInvitationsRepository _invitationsRepository;

    public AcceptInvitationCommandHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        IInvitationsRepository invitationsRepository)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _invitationsRepository = invitationsRepository;
    }

    public async Task<Result> Handle(AcceptInvitationCommand command, CancellationToken ct)
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
            return Result.Failure("You cannot accept this invitation");
        }

        var groupMaybe = await _groupsRepository.GetById(invitation.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure($"Group with id {invitation.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        if (group.Members.Any(x => x.UserId == command.UserId))
        {
            return Result.Failure("User is already a group member");
        }

        var deleteInvitationResult = await _invitationsRepository.Delete(command.InvitationId, ct);

        if (deleteInvitationResult.IsFailure)
        {
            return deleteInvitationResult.ConvertFailure<Result>();
        }

        var now = DateTime.UtcNow;
        var memberId = invitation.GuestId ?? Guid.NewGuid().ToString();

        var newMember = new Member
        {
            Id = memberId,
            UserId = command.UserId,
            Joined = now
        };

        var updatedGroup = group with
        {
            Guests = group.Guests.Where(x => x.Id != memberId).ToList(),
            Members = group.Members.Concat([newMember]).ToList(),
            Updated = now
        };

        var updateGroupResult = await _groupsRepository.Update(updatedGroup, ct);

        if (updateGroupResult.IsFailure)
        {
            return updateGroupResult.ConvertFailure<Result>();
        }

        return Result.Success();
    }
}