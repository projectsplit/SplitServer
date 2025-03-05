using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Commands;

public class RefreshCommandHandler : IRequestHandler<RefreshCommand, Result<AuthenticationResponse>>
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

    public async Task<Result<AuthenticationResponse>> Handle(RefreshCommand command, CancellationToken ct)
    {
        const string errorMessage = "Error while refreshing token";

        var sessionMaybe = await _sessionsRepository.GetByRefreshToken(command.RefreshToken, ct);

        if (sessionMaybe.HasNoValue)
        {
            return Result.Failure<AuthenticationResponse>(errorMessage);
        }

        var session = sessionMaybe.Value;

        if (_authService.HasSessionExpired(session))
        {
            return Result.Failure<AuthenticationResponse>(errorMessage);
        }

        var updatedSession = session with
        {
            Updated = DateTime.UtcNow
        };

        var updateResult = await _sessionsRepository.Update(updatedSession, ct);

        if (updateResult.IsFailure)
        {
            return Result.Failure<AuthenticationResponse>(updateResult.Error);
        }

        return new AuthenticationResponse
        {
            AccessToken = _authService.GenerateAccessToken(session.UserId, session.Id),
            RefreshToken = session.RefreshToken
        };
    }
}