using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.AspNetCore.Identity;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services.Auth;

namespace SplitServer.Commands;

public class SignInWithPasswordCommandHandler : IRequestHandler<SignInWithPasswordCommand, Result<AuthenticationResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly ISessionsRepository _sessionsRepository;
    private readonly AuthService _authService;

    public SignInWithPasswordCommandHandler(
        IUsersRepository usersRepository,
        ISessionsRepository sessionsRepository,
        AuthService authService)
    {
        _usersRepository = usersRepository;
        _sessionsRepository = sessionsRepository;
        _authService = authService;
    }

    public async Task<Result<AuthenticationResponse>> Handle(SignInWithPasswordCommand withPasswordCommand, CancellationToken ct)
    {
        const string credentialErrorMessage = "Invalid credentials";

        var userMaybe = await _usersRepository.GetByUsername(withPasswordCommand.Username, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<AuthenticationResponse>(credentialErrorMessage);
        }

        var user = userMaybe.Value;

        if (user.HashedPassword is null)
        {
            return Result.Failure<AuthenticationResponse>(credentialErrorMessage);
        }

        var hasher = new PasswordHasher<string>();

        var passwordVerificationResult = hasher.VerifyHashedPassword(user.Id, user.HashedPassword, withPasswordCommand.Password);

        if (passwordVerificationResult is PasswordVerificationResult.Failed)
        {
            return Result.Failure<AuthenticationResponse>(credentialErrorMessage);
        }

        var now = DateTime.UtcNow;

        var refreshToken = Guid.NewGuid().ToString();

        var newSession = new Session
        {
            Id = Guid.NewGuid().ToString(),
            Created = now,
            Updated = now,
            UserId = user.Id,
            RefreshToken = refreshToken,
        };

        var writeSessionResult = await _sessionsRepository.Insert(newSession, ct);

        if (writeSessionResult.IsFailure)
        {
            return Result.Failure<AuthenticationResponse>(writeSessionResult.Error);
        }

        return new AuthenticationResponse
        {
            RefreshToken = refreshToken,
            AccessToken = _authService.GenerateAccessToken(user.Id, newSession.Id)
        };
    }
}