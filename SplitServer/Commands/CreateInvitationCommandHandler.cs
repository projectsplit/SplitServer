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
        var userMaybe = await _usersRepository.GetById(command.FromId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.FromId} was not found");
        }

        var groupMaybe = await _groupsRepository.GetById(command.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure($"Group with id {command.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        if (group.Members.All(x => x.UserId != command.FromId))
        {
            return Result.Failure("You are not a member of this group");
        }

        if (group.Members.Any(x => x.UserId == command.ToId))
        {
            return Result.Failure("User is already a group member");
        }
        
        var existingInvitationMaybe = await _invitationsRepository.Get(command.FromId, command.ToId, command.GroupId, ct);

        if (existingInvitationMaybe.HasValue)
        {
            return Result.Failure("Invitation already exists");
        }

        var now = DateTime.UtcNow;

        var newInvitation = new Invitation
        {
            Id = Guid.NewGuid().ToString(),
            Created = now,
            Updated = now,
            FromId = command.FromId,
            ToId = command.ToId,
            GroupId = command.GroupId,
            IsDeleted = false
        };
        
        var writeInvitationResult = await _invitationsRepository.Insert(newInvitation, ct);

        if (writeInvitationResult.IsFailure)
        {
            return Result.Failure(writeInvitationResult.Error);
        }

        return Result.Success();
    }
}