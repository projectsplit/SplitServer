using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Options;
using Serilog;
using SplitServer.Configuration;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;
using SplitServer.Services.Auth;
using SplitServer.Services.Auth.Models;

namespace SplitServer.Commands;

public class ProcessGoogleCodeCommandHandler : IRequestHandler<ProcessGoogleCodeCommand, Result<AuthenticationResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly ISessionsRepository _sessionsRepository;
    private readonly AuthService _authService;
    private readonly LockService _lockService;
    private readonly ValidationService _validationService;

    public ProcessGoogleCodeCommandHandler(
        IOptions<AuthSettings> authSettingsOptions,
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

    public async Task<Result<AuthenticationResponse>> Handle(ProcessGoogleCodeCommand command, CancellationToken ct)
    {
        Log.Information("Consuming google auth code: {}", command.Code);

        var googleUserInfoResult = await _authService.GetGoogleUserInfo(command.Code, ct);

        if (googleUserInfoResult.IsFailure)
        {
            return googleUserInfoResult.ConvertFailure<AuthenticationResponse>();
        }

        var googleUserInfo = googleUserInfoResult.Value;

        using var googleIdLock = _lockService.AcquireLock(googleUserInfo.Id);

        var now = DateTime.UtcNow;

        var userResult = await GetOrCreateUser(googleUserInfo, now, ct);

        if (userResult.IsFailure)
        {
            return userResult.ConvertFailure<AuthenticationResponse>();
        }

        var userId = userResult.Value.Id;
        var sessionId = Guid.NewGuid().ToString();
        var refreshToken = Guid.NewGuid().ToString();

        var newSession = new Session
        {
            Id = sessionId,
            Created = now,
            Updated = now,
            UserId = userId,
            RefreshToken = refreshToken,
        };

        var writeResult = await _sessionsRepository.Insert(newSession, ct);

        if (writeResult.IsFailure)
        {
            return writeResult.ConvertFailure<AuthenticationResponse>();
        }

        return new AuthenticationResponse
        {
            RefreshToken = refreshToken,
            AccessToken = _authService.GenerateAccessToken(userId, sessionId)
        };
    }

    private async Task<Result<User>> GetOrCreateUser(GoogleUserInfo googleUserInfo, DateTime now, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetByGoogleId(googleUserInfo.Id, ct);

        if (userMaybe.HasValue)
        {
            return userMaybe.Value;
        }

        var userId = Guid.NewGuid().ToString();

        var generatedUsername = CreateUsernameFromEmail(googleUserInfo.Email, userId);

        var newUser = new User
        {
            Id = userId,
            Created = now,
            Updated = now,
            Email = googleUserInfo.Email,
            HashedPassword = null,
            Username = generatedUsername,
            GoogleId = googleUserInfo.Id
        };

        var writeResult = await _usersRepository.Insert(newUser, ct);

        if (writeResult.IsFailure)
        {
            return writeResult.ConvertFailure<User>();
        }

        return newUser;
    }

    private string CreateUsernameFromEmail(string email, string userId)
    {
        var prefixWithValidChars = email
            .Split('@')[0]
            .Where(x => _validationService.UsernameAllowedChars.Contains(x))
            .ToArray();

        var validUsername = string.Concat(prefixWithValidChars);

        if (validUsername.Length is >= ValidationService.UsernameMinLength and <= ValidationService.UsernameMaxLength)
        {
            return validUsername;
        }

        return string.Concat(validUsername.Take(12).Concat(userId.Take(4)));
    }
}