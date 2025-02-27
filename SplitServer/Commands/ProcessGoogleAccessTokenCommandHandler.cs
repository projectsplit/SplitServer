using System.Net.Http.Headers;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using SplitServer.Configuration;
using SplitServer.Dto;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class ProcessGoogleAccessTokenCommandHandler : IRequestHandler<ProcessGoogleAccessTokenCommand, Result<AuthTokensResult>>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthSettings _authSettings;
    private readonly IUsersRepository _usersRepository;
    private readonly ISessionsRepository _sessionsRepository;
    private readonly AuthService _authService;
    private readonly LockService _lockService;

    public ProcessGoogleAccessTokenCommandHandler(
        IHttpClientFactory httpClientFactory,
        IOptions<AuthSettings> authSettingsOptions,
        IUsersRepository usersRepository,
        ISessionsRepository sessionsRepository,
        AuthService authService,
        LockService lockService)
    {
        _httpClientFactory = httpClientFactory;
        _usersRepository = usersRepository;
        _sessionsRepository = sessionsRepository;
        _authService = authService;
        _lockService = lockService;
        _authSettings = authSettingsOptions.Value;
    }

    public async Task<Result<AuthTokensResult>> Handle(ProcessGoogleAccessTokenCommand command, CancellationToken ct)
    {
        var googleUserInfoResult = await GetUserInfo(command.GoogleAccessToken, ct);

        if (googleUserInfoResult.IsFailure)
        {
            return googleUserInfoResult.ConvertFailure<AuthTokensResult>();
        }

        var googleId = googleUserInfoResult.Value.Id;

        using var _ = _lockService.AcquireLock(googleId);

        var now = DateTime.UtcNow;

        var userResult = await GetOrCreateUser(googleUserInfoResult.Value, now, ct);

        if (userResult.IsFailure)
        {
            return userResult.ConvertFailure<AuthTokensResult>();
        }

        var userId = userResult.Value.Id;
        var sessionId = Guid.NewGuid().ToString();
        var refreshToken = Guid.NewGuid().ToString();

        var newSession = new Session
        {
            Id = sessionId,
            IsDeleted = false,
            Created = now,
            Updated = now,
            UserId = userId,
            RefreshToken = refreshToken,
        };

        var writeResult = await _sessionsRepository.Insert(newSession, ct);

        if (writeResult.IsFailure)
        {
            return writeResult.ConvertFailure<AuthTokensResult>();
        }

        return new AuthTokensResult
        {
            RefreshToken = refreshToken,
            AccessToken = _authService.GenerateAccessToken(userId, sessionId)
        };
    }

    private async Task<Result<GoogleUserInfoResponse>> GetUserInfo(string googleAccessToken, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, _authSettings.GoogleUserInfoEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, googleAccessToken);

        var response = await _httpClientFactory.CreateClient().SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            return Result.Failure<GoogleUserInfoResponse>("Google user info could not be retrieved");
        }

        var googleUserInfo = await response.Content.ReadFromJsonAsync<GoogleUserInfoResponse>(ct);

        if (googleUserInfo is null)
        {
            return Result.Failure<GoogleUserInfoResponse>("Unable to process Google user info response");
        }

        return googleUserInfo;
    }

    private async Task<Result<User>> GetOrCreateUser(GoogleUserInfoResponse googleUserInfo, DateTime now, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetByGoogleId(googleUserInfo.Id, ct);

        if (userMaybe.HasValue)
        {
            return userMaybe.Value;
        }

        var userId = Guid.NewGuid().ToString();

        var newUser = new User
        {
            Id = userId,
            IsDeleted = false,
            Created = now,
            Updated = now,
            Email = googleUserInfo.Email,
            HashedPassword = null,
            Username = googleUserInfo.Name ?? googleUserInfo.Email.Split('@')[0],
            GoogleId = googleUserInfo.Id
        };

        var writeResult = await _usersRepository.Insert(newUser, ct);

        if (writeResult.IsFailure)
        {
            return writeResult.ConvertFailure<User>();
        }

        return newUser;
    }
}