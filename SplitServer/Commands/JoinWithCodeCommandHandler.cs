using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Commands;

public class JoinWithCodeCommandHandler : IRequestHandler<JoinWithCodeCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IJoinCodesRepository _joinCodesRepository;
    private readonly IGroupsRepository _groupsRepository;

    public JoinWithCodeCommandHandler(
        IUsersRepository usersRepository,
        IJoinCodesRepository joinCodesRepository,
        IGroupsRepository groupsRepository)
    {
        _usersRepository = usersRepository;
        _joinCodesRepository = joinCodesRepository;
        _groupsRepository = groupsRepository;
    }

    public async Task<Result> Handle(JoinWithCodeCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }

        var normalizedJoinCode = command.Code.ToLowerInvariant();

        var joinCodeMaybe = await _joinCodesRepository.GetById(normalizedJoinCode, ct);

        if (joinCodeMaybe.HasNoValue)
        {
            return Result.Failure($"Join token {command.Code} was not found");
        }

        var joinCode = joinCodeMaybe.Value;

        if (joinCode.Expires < DateTime.UtcNow)
        {
            return Result.Failure($"Join token {command.Code} is expired");
        }

        if (joinCode.TimesUsed >= joinCode.MaxUses)
        {
            return Result.Failure($"Join token {command.Code} has reached maximum number of uses");
        }

        var groupMaybe = await _groupsRepository.GetById(joinCode.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure($"Group with id {joinCode.GroupId} was not found");
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

        var updatedJoinCode = joinCode with
        {
            TimesUsed = joinCode.TimesUsed + 1,
            Updated = now
        };

        return await _joinCodesRepository.Update(updatedJoinCode, ct);
    }
}