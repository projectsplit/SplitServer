using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Commands;

public class CreateInvitationCommandHandler : IRequestHandler<CreateInvitationCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IInvitationsRepository _invitationsRepository;

    public CreateInvitationCommandHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        IInvitationsRepository invitationsRepository)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _invitationsRepository = invitationsRepository;
    }

    public async Task<Result> Handle(CreateInvitationCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }

        var groupMaybe = await _groupsRepository.GetById(command.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure($"Group with id {command.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        if (group.Members.All(x => x.UserId != command.UserId))
        {
            return Result.Failure("You are not a member of this group");
        }

        if (group.Members.Any(x => x.UserId == command.ToId))
        {
            return Result.Failure("User is already a group member");
        }

        if (command.GuestId is not null)
        {
            if (group.Guests.All(x => x.Id != command.GuestId))
            {
                return Result.Failure("Guest is not a group member");
            }

            var existingInvitationByGuestIdMaybe = await _invitationsRepository.GetByGuestId(command.GuestId, command.GroupId, ct);

            if (existingInvitationByGuestIdMaybe.HasValue)
            {
                var deleteInvitationByGuestIdResult = await _invitationsRepository.Delete(existingInvitationByGuestIdMaybe.Value.Id, ct);

                if (deleteInvitationByGuestIdResult.IsFailure)
                {
                    return deleteInvitationByGuestIdResult;
                }
            }
        }

        var existingInvitationByToIdMaybe = await _invitationsRepository.GetByToId(command.ToId, command.GroupId, ct);

        if (existingInvitationByToIdMaybe.HasValue)
        {
            var deleteInvitationByToIdResult = await _invitationsRepository.Delete(existingInvitationByToIdMaybe.Value.Id, ct);

            if (deleteInvitationByToIdResult.IsFailure)
            {
                return deleteInvitationByToIdResult;
            }
        }

        var now = DateTime.UtcNow;

        var newInvitation = new Invitation
        {
            Id = Guid.NewGuid().ToString(),
            Created = now,
            Updated = now,
            FromId = command.UserId,
            ToId = command.ToId,
            GroupId = command.GroupId,
            GuestId = command.GuestId,
            IsDeleted = false,
        };

        var writeResult = await _invitationsRepository.Insert(newInvitation, ct);

        if (writeResult.IsFailure)
        {
            return Result.Failure(writeResult.Error);
        }

        return Result.Success();
    }
}