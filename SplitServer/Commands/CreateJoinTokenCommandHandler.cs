using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Options;
using SplitServer.Configuration;
using SplitServer.Dto;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class CreateJoinTokenCommandHandler : IRequestHandler<CreateJoinTokenCommand, Result<CreateJoinTokenResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IJoinTokensRepository _joinTokensRepository;
    private readonly int _maxTokenUses;
    private readonly int _tokenExpirationInSeconds;
    private readonly int _tokenLength;

    public CreateJoinTokenCommandHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        IJoinTokensRepository joinTokensRepository,
        IOptions<JoinSettings> joinSettings)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _joinTokensRepository = joinTokensRepository;
        _maxTokenUses = joinSettings.Value.MaxTokenUses;
        _tokenExpirationInSeconds = joinSettings.Value.TokenExpirationInSeconds;
        _tokenLength = joinSettings.Value.TokenLength;
    }

    public async Task<Result<CreateJoinTokenResponse>> Handle(CreateJoinTokenCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<CreateJoinTokenResponse>($"User with id {command.UserId} was not found");
        }

        var groupMaybe = await _groupsRepository.GetById(command.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure<CreateJoinTokenResponse>($"Group with id {command.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        if (group.Members.All(x => x.UserId != command.UserId))
        {
            return Result.Failure<CreateJoinTokenResponse>("You are not a member of this group");
        }

        var now = DateTime.UtcNow;

        var joinToken = new JoinToken
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

        var writeResult = await _joinTokensRepository.Insert(joinToken, ct);

        if (writeResult.IsFailure)
        {
            return writeResult.ConvertFailure<CreateJoinTokenResponse>();
        }

        return new CreateJoinTokenResponse
        {
            JoinToken = joinToken.Id
        };
    }
}