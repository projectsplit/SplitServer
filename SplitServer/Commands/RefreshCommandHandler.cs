using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class RefreshCommandHandler : IRequestHandler<RefreshCommand, Result<AuthTokensResult>>
{
    private readonly ISessionsRepository _sessionsRepository;
    private readonly AuthService _authService;

    public RefreshCommandHandler(
        ISessionsRepository sessionsRepository,
        AuthService authService)
    {
        _sessionsRepository = sessionsRepository;
        _authService = authService;
    }

    public async Task<Result<AuthTokensResult>> Handle(RefreshCommand command, CancellationToken ct)
    {
        const string errorMessage = "Error while refreshing token";

        var sessionMaybe = await _sessionsRepository.GetByRefreshToken(command.RefreshToken, ct);

        if (sessionMaybe.HasNoValue)
        {
            return Result.Failure<AuthTokensResult>(errorMessage);
        }

        // if (sessionMaybe.HasNoValue)
        // {
        //     var faultySessionMaybe = await _sessionsRepository.GetByPreviousRefreshToken(command.RefreshToken, ct);
        //
        //     if (faultySessionMaybe.HasNoValue)
        //     {
        //         return Result.Failure<AuthTokensResult>(errorMessage);
        //     }
        //
        //     var deleteFaultySessionResult = await _sessionsRepository.Delete(faultySessionMaybe.Value.Id, ct);
        //
        //     return Result.Failure<AuthTokensResult>(deleteFaultySessionResult.IsFailure ? deleteFaultySessionResult.Error : errorMessage);
        // }

        var session = sessionMaybe.Value;

        // var newRefreshToken = Guid.NewGuid().ToString();
        // var updatedPreviousRefreshTokens = session
        //     .PreviousRefreshTokens
        //     .Concat(new List<string> { session.RefreshToken })
        //     .ToList();

        var updatedSession = new Session
        {
            Id = session.Id,
            Created = session.Created,
            Updated = DateTime.UtcNow,
            UserId = session.UserId,
            RefreshToken = session.RefreshToken,
            PreviousRefreshTokens = [],
            IsDeleted = session.IsDeleted
        };
        
        var updateResult = await _sessionsRepository.Update(updatedSession, ct);

        if (updateResult.IsFailure)
        {
            return Result.Failure<AuthTokensResult>(updateResult.Error);
        }

        return new AuthTokensResult
        {
            AccessToken = _authService.GenerateAccessToken(session.UserId, session.Id),
            RefreshToken = session.RefreshToken
        };
    }
}