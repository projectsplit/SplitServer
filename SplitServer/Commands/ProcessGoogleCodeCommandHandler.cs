using CSharpFunctionalExtensions;
using MediatR;
using Serilog;
using SplitServer.Responses;
using SplitServer.Services.Auth;

namespace SplitServer.Commands;

public class ProcessGoogleCodeCommandHandler : IRequestHandler<ProcessGoogleCodeCommand, Result<AuthenticationResponse>>
{
    private readonly AuthService _authService;
    private readonly GoogleAccountService _googleAccountService;

    public ProcessGoogleCodeCommandHandler(
        AuthService authService,
        GoogleAccountService googleAccountService)
    {
        _authService = authService;
        _googleAccountService = googleAccountService;
    }

    public async Task<Result<AuthenticationResponse>> Handle(ProcessGoogleCodeCommand command, CancellationToken ct)
    {
        Log.Information("Consuming google auth code: {0}", command.Code);

        var googleUserInfoResult = await _authService.GetGoogleUserInfo(command.Code, ct);

        if (googleUserInfoResult.IsFailure)
        {
            return googleUserInfoResult.ConvertFailure<AuthenticationResponse>();
        }

        return await _googleAccountService.SignIn(googleUserInfoResult.Value, ct);
    }
}
