using CSharpFunctionalExtensions;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services.Auth.Models;

namespace SplitServer.Services.Auth;

/// <summary>
/// Turns a verified Google identity into a signed-in session. The browser proves that identity by
/// exchanging an auth code and the Android app by presenting an id token from Google's native sheet,
/// but both arrive here with the same <see cref="GoogleUserInfo"/> and must resolve to the same
/// account. Account linking is the part that decides who owns an address, so it lives in one place
/// rather than being copied per entry point where the two copies could drift apart.
/// </summary>
public class GoogleAccountService
{
    private readonly IUsersRepository _usersRepository;
    private readonly ISessionsRepository _sessionsRepository;
    private readonly AuthService _authService;
    private readonly LockService _lockService;
    private readonly ValidationService _validationService;

    public GoogleAccountService(
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

    public async Task<Result<AuthenticationResponse>> SignIn(GoogleUserInfo googleUserInfo, CancellationToken ct)
    {
        using var _ = _lockService.AcquireLock(googleUserInfo.Id);

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

        // Signing in with a Google account Google has proven is a claim on this address, whether it
        // ends in a link or a new account. Hold the lock verification uses across both so the two
        // paths cannot claim it at once. An unproven address claims nothing, so it takes no lock.
        using var emailLock = googleUserInfo.EmailVerified
            ? _lockService.AcquireLock($"verify-email:{googleUserInfo.Email.ToLowerInvariant()}")
            : null;

        // Only Google having proven the address makes it safe to find an existing account by it.
        // Linking on an unproven address would hand that account to anyone who can put the address
        // on a Google profile, so instead we fall through and create an ordinary unverified account.
        if (googleUserInfo.EmailVerified)
        {
            var verifiedOwnerMaybe = await _usersRepository.GetVerifiedByEmail(googleUserInfo.Email, ct);

            if (verifiedOwnerMaybe.HasValue)
            {
                // Google has proven this address and the existing account has too, so there is nothing
                // to create: attach the Google identity to it. A second account would break the
                // single-owner rule that password reset and username recovery depend on.
                var existingUser = verifiedOwnerMaybe.Value;

                if (existingUser.GoogleId is not null)
                {
                    return Result.Failure<User>("This email is already associated with another account");
                }

                var linkedUser = existingUser with
                {
                    GoogleId = googleUserInfo.Id,
                    Updated = now,
                };

                var linkResult = await _usersRepository.Update(linkedUser, ct);

                return linkResult.IsFailure ? linkResult.ConvertFailure<User>() : linkedUser;
            }
        }

        var userId = Guid.NewGuid().ToString();

        var generatedUsername = CreateUsernameFromEmail(googleUserInfo.Email, userId);

        var newUser = new User
        {
            Id = userId,
            Created = now,
            Updated = now,
            Email = googleUserInfo.Email,
            EmailVerified = googleUserInfo.EmailVerified,
            HashedPassword = null,
            Username = generatedUsername,
            GoogleId = googleUserInfo.Id,
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
