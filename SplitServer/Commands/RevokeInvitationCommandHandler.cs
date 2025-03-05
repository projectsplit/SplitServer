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

        var groupMaybe = await _groupsRepository.GetById(command.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure($"Group with id {command.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        if (group.Members.All(x => x.UserId != command.UserId))
        {
            return Result.Failure("You must be a group member to revoke invitations");
        }

        return await _invitationsRepository.DeleteByGroupIdAndReceiverId(command.ReceiverId, command.GroupId, ct);
    }
}