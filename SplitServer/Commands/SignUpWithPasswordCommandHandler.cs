using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.AspNetCore.Identity;
using SplitServer.Dto;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class SignUpWithPasswordCommandHandler : IRequestHandler<SignUpWithPasswordCommand, Result<AuthTokensResult>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly ISessionsRepository _sessionsRepository;
    private readonly AuthService _authService;
    private readonly LockService _lockService;

    public SignUpWithPasswordCommandHandler(
        IUsersRepository usersRepository,
        ISessionsRepository sessionsRepository,
        AuthService authService,
        LockService lockService)
    {
        _usersRepository = usersRepository;
        _sessionsRepository = sessionsRepository;
        _authService = authService;
        _lockService = lockService;
    }

    public async Task<Result<AuthTokensResult>> Handle(SignUpWithPasswordCommand command, CancellationToken ct)
    {
        using var _ = _lockService.AcquireLock(command.Username);

        var userMaybe = await _usersRepository.GetByEmail(command.Username, ct);

        if (userMaybe.HasValue)
        {
            return Result.Failure<AuthTokensResult>("User with this username already exists");
        }

        var userId = Guid.NewGuid().ToString();

        var hasher = new PasswordHasher<string>();

        var hashedPassword = hasher.HashPassword(userId, command.Password);

        var now = DateTime.UtcNow;

        var newUser = new User
        {
            Id = userId,
            IsDeleted = false,
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
            return Result.Failure<AuthTokensResult>(writeUserResult.Error);
        }

        var refreshToken = Guid.NewGuid().ToString();

        var newSession = new Session
        {
            Id = Guid.NewGuid().ToString(),
            Created = now,
            Updated = now,
            UserId = newUser.Id,
            RefreshToken = refreshToken,
            IsDeleted = false
        };

        var writeSessionResult = await _sessionsRepository.Insert(newSession, ct);

        if (writeSessionResult.IsFailure)
        {
            return Result.Failure<AuthTokensResult>(writeSessionResult.Error);
        }

        return new AuthTokensResult
        {
            RefreshToken = refreshToken,
            AccessToken = _authService.GenerateAccessToken(newUser.Id, newSession.Id)
        };
    }
}