using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Commands;

public class UseJoinTokenCommandHandler : IRequestHandler<UseJoinTokenCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IJoinTokensRepository _joinTokensRepository;
    private readonly IGroupsRepository _groupsRepository;

    public UseJoinTokenCommandHandler(
        IUsersRepository usersRepository,
        IJoinTokensRepository joinTokensRepository,
        IGroupsRepository groupsRepository)
    {
        _usersRepository = usersRepository;
        _joinTokensRepository = joinTokensRepository;
        _groupsRepository = groupsRepository;
    }

    public async Task<Result> Handle(UseJoinTokenCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }

        var joinTokenMaybe = await _joinTokensRepository.GetById(command.JoinToken, ct);

        if (joinTokenMaybe.HasNoValue)
        {
            return Result.Failure($"Join token {command.JoinToken} was not found");
        }

        var joinToken = joinTokenMaybe.Value;

        if (joinToken.Expires < DateTime.UtcNow)
        {
            return Result.Failure($"Join token {command.JoinToken} is expired");
        }

        if (joinToken.TimesUsed >= joinToken.MaxUses)
        {
            return Result.Failure($"Join token {command.JoinToken} has reached maximum number of uses");
        }

        var groupMaybe = await _groupsRepository.GetById(joinToken.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure($"Group with id {joinToken.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        if (group.Members.Any(x => x.UserId == command.UserId))
        {
            return Result.Failure("You are already a group member");
        }

        var now = DateTime.UtcNow;

        var newMember = new Member
        {
            Id = Guid.NewGuid().ToString(),
            UserId = command.UserId,
            Joined = now
        };

        var updatedGroup = group with
        {
            Members = group.Members.Concat([newMember]).ToList(),
            Updated = now
        };

        var updateGroupResult = await _groupsRepository.Update(updatedGroup, ct);

        if (updateGroupResult.IsFailure)
        {
            return updateGroupResult;
        }

        var updatedJoinToken = joinToken with
        {
            TimesUsed = joinToken.TimesUsed + 1,
            Updated = now
        };

        var updateJoinTokenResult = await _joinTokensRepository.Update(updatedJoinToken, ct);

        if (updateJoinTokenResult.IsFailure)
        {
            return updateJoinTokenResult;
        }

        return Result.Success();
    }
}