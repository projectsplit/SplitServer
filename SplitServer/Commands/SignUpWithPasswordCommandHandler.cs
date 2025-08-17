using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;
using SplitServer.Services.Auth;

namespace SplitServer.Commands;

public class SignUpWithPasswordCommandHandler : IRequestHandler<SignUpWithPasswordCommand, Result<AuthenticationResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly ISessionsRepository _sessionsRepository;
    private readonly AuthService _authService;
    private readonly LockService _lockService;
    private readonly ValidationService _validationService;

    public SignUpWithPasswordCommandHandler(
        IUsersRepository usersRepository,
        ISessionsRepository sessionsRepository,
        AuthService authService,
        LockService lockService,
        ValidationService validationService)
    {
        _usersRepository = usersRepository;
        _sessionsRepository = sessionsRepository;
        _authService = authService;
        _lockService = lockService;
        _validationService = validationService;
    }

    public async Task<Result<AuthenticationResponse>> Handle(SignUpWithPasswordCommand command, CancellationToken ct)
    {
        using var _ = _lockService.AcquireLock(command.Username);

        if (await _usersRepository.AnyWithUsername(command.Username, ct))
        {
            return Result.Failure<AuthenticationResponse>("User with this username already exists");
        }
        var usernameValidationResult = _validationService.ValidateUsername(command.Username);

        if (usernameValidationResult.IsFailure)
        {
            return Result.Failure<AuthenticationResponse>(usernameValidationResult.Error);
        }
        
        var userId = Guid.NewGuid().ToString();

        var hasher = new PasswordHasher<string>();

        var hashedPassword = hasher.HashPassword(userId, command.Password);

        var now = DateTime.UtcNow;

        var newUser = new User
        {
            Id = userId,
            Created = now,
            Updated = now,
            Email = null,
            HashedPassword = hashedPassword,
            Username = command.Username,
            GoogleId = null
        };

        var writeUserResult = await _usersRepository.Insert(newUser, ct);

        if (writeUserResult.IsFailure)
        {
            return Result.Failure<AuthenticationResponse>(writeUserResult.Error);
        }

        var refreshToken = Guid.NewGuid().ToString();

        var newSession = new Session
        {
            Id = Guid.NewGuid().ToString(),
            Created = now,
            Updated = now,
            UserId = newUser.Id,
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
            AccessToken = _authService.GenerateAccessToken(newUser.Id, newSession.Id)
        };
    }
}