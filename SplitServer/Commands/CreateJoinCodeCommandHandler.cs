using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Options;
using SplitServer.Configuration;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Commands;

public class CreateJoinCodeCommandHandler : IRequestHandler<CreateJoinCodeCommand, Result<CreateJoinCodeResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IJoinCodesRepository _joinCodesRepository;
    private readonly int _maxTokenUses;
    private readonly int _tokenExpirationInSeconds;
    private readonly int _tokenLength;

    public CreateJoinCodeCommandHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        IJoinCodesRepository joinCodesRepository,
        IOptions<JoinSettings> joinSettings)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _joinCodesRepository = joinCodesRepository;
        _maxTokenUses = joinSettings.Value.MaxTokenUses;
        _tokenExpirationInSeconds = joinSettings.Value.TokenExpirationInSeconds;
        _tokenLength = joinSettings.Value.TokenLength;
    }

    public async Task<Result<CreateJoinCodeResponse>> Handle(CreateJoinCodeCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<CreateJoinCodeResponse>($"User with id {command.UserId} was not found");
        }

        var groupMaybe = await _groupsRepository.GetById(command.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure<CreateJoinCodeResponse>($"Group with id {command.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        if (group.Members.All(x => x.UserId != command.UserId))
        {
            return Result.Failure<CreateJoinCodeResponse>("You are not a member of this group");
        }

        var now = DateTime.UtcNow;

        var joinCode = new JoinCode
        {
            Id = JoinService.GenerateToken(_tokenLength),
            IsDeleted = false,
            Created = now,
            Updated = now,
            GroupId = command.GroupId,
            CreatorId = command.UserId,
            TimesUsed = 0,
            MaxUses = _maxTokenUses,
            Expires = now + TimeSpan.FromSeconds(_tokenExpirationInSeconds),
        };

        var writeResult = await _joinCodesRepository.Insert(joinCode, ct);

        if (writeResult.IsFailure)
        {
            return writeResult.ConvertFailure<CreateJoinCodeResponse>();
        }

        return new CreateJoinCodeResponse
        {
            Code = joinCode.Id
        };
    }
}