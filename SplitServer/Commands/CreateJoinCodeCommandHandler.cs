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
    private readonly PermissionService _permissionService;
    private readonly IJoinCodesRepository _joinCodesRepository;
    private readonly int _maxTokenUses;
    private readonly int _joinCodeExpirationInSeconds;
    private readonly int _tokenLength;

    public CreateJoinCodeCommandHandler(
        IJoinCodesRepository joinCodesRepository,
        IOptions<JoinSettings> joinSettings,
        PermissionService permissionService)
    {
        _joinCodesRepository = joinCodesRepository;
        _permissionService = permissionService;
        _maxTokenUses = joinSettings.Value.MaxTokenUses;
        _joinCodeExpirationInSeconds = joinSettings.Value.TokenExpirationInSeconds;
        _tokenLength = joinSettings.Value.TokenLength;
    }

    public async Task<Result<CreateJoinCodeResponse>> Handle(CreateJoinCodeCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyGroupAction(command.UserId, command.GroupId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult.ConvertFailure<CreateJoinCodeResponse>();
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
            Expires = now + TimeSpan.FromSeconds(_joinCodeExpirationInSeconds),
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