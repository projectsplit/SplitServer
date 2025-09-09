using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;

namespace SplitServer.Commands;

public class RevokeJoinCodeCommandHandler : IRequestHandler<RevokeJoinCodeCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IJoinCodesRepository _joinCodesRepository;
    private readonly IGroupsRepository _groupsRepository;

    public RevokeJoinCodeCommandHandler(
        IUsersRepository usersRepository,
        IJoinCodesRepository joinCodesRepository,
        IGroupsRepository groupsRepository)
    {
        _usersRepository = usersRepository;
        _joinCodesRepository = joinCodesRepository;
        _groupsRepository = groupsRepository;
    }

    public async Task<Result> Handle(RevokeJoinCodeCommand command, CancellationToken ct)
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

        var groupMaybe = await _groupsRepository.GetById(joinCode.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure($"Group with id {joinCode.GroupId} was not found");
        }

        return await _joinCodesRepository.Delete(normalizedJoinCode, ct);
    }
}