using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Commands;

public class RevokeJoinTokenCommandHandler : IRequestHandler<RevokeJoinTokenCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IJoinTokensRepository _joinTokensRepository;
    private readonly IGroupsRepository _groupsRepository;

    public RevokeJoinTokenCommandHandler(
        IUsersRepository usersRepository,
        IJoinTokensRepository joinTokensRepository,
        IGroupsRepository groupsRepository)
    {
        _usersRepository = usersRepository;
        _joinTokensRepository = joinTokensRepository;
        _groupsRepository = groupsRepository;
    }

    public async Task<Result> Handle(RevokeJoinTokenCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }

        var normalizedJoinToken = command.JoinToken.ToLowerInvariant();

        var joinTokenMaybe = await _joinTokensRepository.GetById(normalizedJoinToken, ct);

        if (joinTokenMaybe.HasNoValue)
        {
            return Result.Failure($"Join token {command.JoinToken} was not found");
        }

        var joinToken = joinTokenMaybe.Value;

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

        var updateJoinTokenResult = await _joinTokensRepository.Delete(normalizedJoinToken, ct);

        if (updateJoinTokenResult.IsFailure)
        {
            return updateJoinTokenResult;
        }

        return Result.Success();
    }
}