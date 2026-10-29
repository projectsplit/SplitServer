using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SplitServer.Commands;
using SplitServer.Configuration;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;
using SplitServer.Services.Auth;
using SplitServer.Services.Email;

namespace SplitServer.Tests;

/// <summary>
/// Signing up and resetting a password used to accept whatever the client sent, because only the
/// app's own forms checked the length. These hold both to the minimum that changing a password
/// applies, at the boundary: a password one character short is refused with nothing written, and
/// one of exactly the minimum goes through.
/// </summary>
public class PasswordMinimumTests
{
    private const string UserId = "user-1";
    private const string ResetToken = "reset-token";

    private static readonly string OneShort = new('a', ValidationService.PasswordMinLength - 1);
    private static readonly string ExactlyTheMinimum = new('a', ValidationService.PasswordMinLength);

    [Fact]
    public async Task Sign_up_refuses_a_password_one_character_short_and_creates_nothing()
    {
        var users = new FakeUsersRepository();
        var sessions = new FakeSessionsRepository();

        var result = await SignUpHandler(users, sessions).Handle(SignUp(OneShort), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Null(users.Inserted);
        Assert.Null(sessions.Inserted);
    }

    [Fact]
    public async Task Sign_up_accepts_a_password_of_exactly_the_minimum()
    {
        var users = new FakeUsersRepository();
        var sessions = new FakeSessionsRepository();

        var result = await SignUpHandler(users, sessions).Handle(SignUp(ExactlyTheMinimum), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : string.Empty);
        Assert.NotNull(users.Inserted);
        Assert.True(Verifies(users.Inserted, ExactlyTheMinimum));
        Assert.NotNull(sessions.Inserted);
    }

    [Fact]
    public async Task Reset_refuses_a_password_one_character_short_and_leaves_the_link_unused()
    {
        var users = new FakeUsersRepository(ExistingUser());
        var codes = new FakeCodesRepository(ActiveResetCode());
        var sessions = new FakeSessionsRepository();

        var result = await ResetHandler(users, codes, sessions).Handle(Reset(OneShort), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Null(users.Updated);
        Assert.Null(codes.Consumed);
        Assert.Null(sessions.SignedOutUserId);
    }

    [Fact]
    public async Task Reset_accepts_a_password_of_exactly_the_minimum()
    {
        var users = new FakeUsersRepository(ExistingUser());
        var codes = new FakeCodesRepository(ActiveResetCode());
        var sessions = new FakeSessionsRepository();

        var result = await ResetHandler(users, codes, sessions).Handle(Reset(ExactlyTheMinimum), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : string.Empty);
        Assert.NotNull(users.Updated);
        Assert.True(Verifies(users.Updated, ExactlyTheMinimum));
        Assert.NotNull(codes.Consumed?.ConsumedAt);
        Assert.Equal(UserId, sessions.SignedOutUserId);
    }

    private static SignUpWithPasswordCommand SignUp(string password)
    {
        return new SignUpWithPasswordCommand
        {
            Username = "someone",
            Email = "someone@example.com",
            Password = password,
        };
    }

    private static ResetPasswordCommand Reset(string newPassword)
    {
        return new ResetPasswordCommand
        {
            Token = ResetToken,
            NewPassword = newPassword,
        };
    }

    private static SignUpWithPasswordCommandHandler SignUpHandler(FakeUsersRepository users, FakeSessionsRepository sessions)
    {
        var authSettings = new AuthSettings
        {
            SectionName = "Auth",
            Key = "a-test-signing-key-that-is-long-enough-for-hs256",
            Issuer = "test",
            Audience = "test",
            AccessTokenDurationInMinutes = 5,
            RefreshTokenDurationInMinutes = 60,
            RefreshTokenCookieName = "refresh",
            RefreshEndpointPath = "/auth/refresh",
            RefreshTokenCookieSecure = true,
            RefreshTokenCookieHttpOnly = true,
            RefreshTokenCookieSameSite = SameSiteMode.Strict,
            ClientUrl = "http://localhost",
            ClientGoogleRedirectUri = "/google",
            GoogleTokenEndpoint = "http://localhost",
            GoogleClientId = "client",
            GoogleClientSecret = "secret",
            GoogleJsonWebKeySetEndpoint = "http://localhost",
            GoogleIdTokenIssuer = "issuer",
            ApiKey = "api-key",
            AllowPasswordSignup = true,
        };

        // Signing up never calls out over HTTP; only the Google flows need the client factory.
        var authService = new AuthService(Options.Create(authSettings), null!);

        return new SignUpWithPasswordCommandHandler(users, sessions, authService, new LockService(), new ValidationService());
    }

    private static ResetPasswordCommandHandler ResetHandler(
        FakeUsersRepository users,
        FakeCodesRepository codes,
        FakeSessionsRepository sessions)
    {
        return new ResetPasswordCommandHandler(users, codes, sessions, new EmailTokenService(), new ValidationService());
    }

    private static User ExistingUser()
    {
        return new User
        {
            Id = UserId,
            Username = "someone",
            Email = "someone@example.com",
            EmailVerified = true,
            HashedPassword = new PasswordHasher<string>().HashPassword(UserId, "the forgotten password"),
            GoogleId = null,
            Created = DateTime.UnixEpoch,
            Updated = DateTime.UnixEpoch,
        };
    }

    private static EmailVerificationCode ActiveResetCode()
    {
        return new EmailVerificationCode
        {
            Id = "code-1",
            UserId = UserId,
            CodeHash = new EmailTokenService().Hash(ResetToken),
            Purpose = EmailVerificationCodePurpose.PasswordReset,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            ConsumedAt = null,
            Created = DateTime.UnixEpoch,
            Updated = DateTime.UnixEpoch,
        };
    }

    private static bool Verifies(User user, string password)
    {
        return user.HashedPassword is not null &&
               new PasswordHasher<string>().VerifyHashedPassword(user.Id, user.HashedPassword, password) is not PasswordVerificationResult.Failed;
    }

    private class FakeUsersRepository : IUsersRepository
    {
        private readonly User? _existing;

        public User? Inserted { get; private set; }

        public User? Updated { get; private set; }

        public FakeUsersRepository(User? existing = null) => _existing = existing;

        public Task<bool> AnyWithUsername(string username, CancellationToken ct) => Task.FromResult(false);

        public Task<Maybe<User>> GetVerifiedByEmail(string email, CancellationToken ct) => Task.FromResult(Maybe<User>.None);

        public Task<Maybe<User>> GetById(string id, CancellationToken ct)
        {
            return Task.FromResult(_existing is not null && _existing.Id == id ? _existing : Maybe<User>.None);
        }

        public Task<Result> Insert(User entity, CancellationToken ct)
        {
            Inserted = entity;

            return Task.FromResult(Result.Success());
        }

        public Task<Result> Update(User updatedEntity, CancellationToken ct)
        {
            Updated = updatedEntity;

            return Task.FromResult(Result.Success());
        }

        public Task<Maybe<User>> GetByUsername(string username, CancellationToken ct) => throw new NotSupportedException();
        public Task<Maybe<User>> GetByGoogleId(string googleId, CancellationToken ct) => throw new NotSupportedException();
        public Task<List<User>> SearchByUsername(string keyword, UserIdScope scope, int skip, int pageSize, CancellationToken ct) => throw new NotSupportedException();
        public Task<List<User>> GetLatestUsers(UserIdScope scope, int skip, int pageSize, CancellationToken ct) => throw new NotSupportedException();
        public Task<IList<User>> GetByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> InsertMany(IEnumerable<User> entities, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> Upsert(User entity, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> Delete(string id, CancellationToken ct) => throw new NotSupportedException();
    }

    private class FakeSessionsRepository : ISessionsRepository
    {
        public Session? Inserted { get; private set; }

        public string? SignedOutUserId { get; private set; }

        public Task<Result> Insert(Session entity, CancellationToken ct)
        {
            Inserted = entity;

            return Task.FromResult(Result.Success());
        }

        public Task<Result> DeleteByUserId(string userId, CancellationToken ct)
        {
            SignedOutUserId = userId;

            return Task.FromResult(Result.Success());
        }

        public Task<Maybe<Session>> GetByRefreshToken(string refreshToken, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> DeleteByRefreshToken(string refreshToken, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> DeleteByUserIdExceptSession(string userId, string sessionId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Maybe<Session>> GetById(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task<IList<Session>> GetByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> InsertMany(IEnumerable<Session> entities, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> Upsert(Session entity, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> Delete(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> Update(Session updatedEntity, CancellationToken ct) => throw new NotSupportedException();
    }

    private class FakeCodesRepository : IEmailVerificationCodesRepository
    {
        private readonly EmailVerificationCode _active;

        public EmailVerificationCode? Consumed { get; private set; }

        public FakeCodesRepository(EmailVerificationCode active) => _active = active;

        public Task<Maybe<EmailVerificationCode>> GetActiveByCodeHash(
            string codeHash,
            EmailVerificationCodePurpose purpose,
            CancellationToken ct)
        {
            var matches = codeHash == _active.CodeHash && purpose == _active.Purpose;

            return Task.FromResult(matches ? _active : Maybe<EmailVerificationCode>.None);
        }

        public Task<Result> Update(EmailVerificationCode updatedEntity, CancellationToken ct)
        {
            Consumed = updatedEntity;

            return Task.FromResult(Result.Success());
        }

        public Task<Result> InvalidateActiveCodes(string userId, EmailVerificationCodePurpose purpose, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> DeleteByUserId(string userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Maybe<EmailVerificationCode>> GetById(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task<IList<EmailVerificationCode>> GetByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> Insert(EmailVerificationCode entity, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> InsertMany(IEnumerable<EmailVerificationCode> entities, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> Upsert(EmailVerificationCode entity, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> Delete(string id, CancellationToken ct) => throw new NotSupportedException();
    }
}
