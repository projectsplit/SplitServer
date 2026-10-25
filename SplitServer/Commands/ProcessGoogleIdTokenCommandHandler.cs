using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;
using SplitServer.Services.Auth;

namespace SplitServer.Commands;

public class ProcessGoogleIdTokenCommandHandler
    : IRequestHandler<ProcessGoogleIdTokenCommand, Result<AuthenticationResponse>>
{
    private readonly AuthService _authService;
    private readonly GoogleAccountService _googleAccountService;

    public ProcessGoogleIdTokenCommandHandler(
        AuthService authService,
        GoogleAccountService googleAccountService)
    {
        _authService = authService;
        _googleAccountService = googleAccountService;
    }

    public async Task<Result<AuthenticationResponse>> Handle(ProcessGoogleIdTokenCommand command, CancellationToken ct)
    {
        // Deliberately not logged: unlike a single-use auth code, an id token stays replayable
        // against this endpoint until it expires, so it must not reach the log.
        var googleUserInfoResult = await _authService.ValidateGoogleIdToken(command.IdToken, ct);

        if (googleUserInfoResult.IsFailure)
        {
            return googleUserInfoResult.ConvertFailure<AuthenticationResponse>();
        }

        return await _googleAccountService.SignIn(googleUserInfoResult.Value, ct);
    }
}
