using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Identity;
using SplitServer.Commands;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Tests;

/// <summary>
/// What these pin down is the gate rather than the change: nothing is written until the current
/// password has checked out, an account without a password is turned away, and a successful change
/// signs out every session but the one that asked for it.
/// </summary>
public class ChangePasswordCommandHandlerTests
{
    private const string UserId = "user-1";
    private const string ThisSessionId = "session-here";
    private const string OtherSessionId = "session-elsewhere";
    private const string CurrentPassword = "correct horse battery";
    private const string NewPassword = "staple xkcd 936";

    [Fact]
    public async Task Wrong_current_password_changes_nothing()
    {
        var (handler, users, sessions) = Build(hasPassword: true);

        var result = await handler.Handle(Command("not the password", NewPassword), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ChangePasswordCommandHandler.WrongCurrentPasswordError, result.Error);
        Assert.Null(users.Updated);
        Assert.Equal(2, sessions.Sessions.Count);
    }

    [Fact]
    public async Task Missing_current_password_counts_as_wrong()
    {
        var (handler, users, sessions) = Build(hasPassword: true);

        var result = await handler.Handle(Command(null!, NewPassword), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ChangePasswordCommandHandler.WrongCurrentPasswordError, result.Error);
        Assert.Null(users.Updated);
        Assert.Equal(2, sessions.Sessions.Count);
    }

    [Fact]
    public async Task Account_without_a_password_is_turned_away()
    {
        var (handler, users, sessions) = Build(hasPassword: false);

        var result = await handler.Handle(Command("", NewPassword), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ChangePasswordCommandHandler.NoPasswordError, result.Error);
        Assert.Null(users.Updated);
        Assert.Equal(2, sessions.Sessions.Count);
    }

    [Fact]
    public async Task Short_new_password_is_refused()
    {
        var (handler, users, _) = Build(hasPassword: true);

        var result = await handler.Handle(Command(CurrentPassword, "short"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Null(users.Updated);
    }

    [Fact]
    public async Task Same_password_again_is_refused()
    {
        var (handler, users, _) = Build(hasPassword: true);

        var result = await handler.Handle(Command(CurrentPassword, CurrentPassword), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ChangePasswordCommandHandler.SamePasswordError, result.Error);
        Assert.Null(users.Updated);
    }

    [Fact]
    public async Task Correct_current_password_replaces_the_hash_and_keeps_only_this_session()
    {
        var (handler, users, sessions) = Build(hasPassword: true);

        var result = await handler.Handle(Command(CurrentPassword, NewPassword), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : string.Empty);

        var updated = users.Updated;
        Assert.NotNull(updated);
        Assert.NotNull(updated.HashedPassword);

        var hasher = new PasswordHasher<string>();
        Assert.NotEqual(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(UserId, updated.HashedPassword, NewPassword));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(UserId, updated.HashedPassword, CurrentPassword));

        Assert.Equal([ThisSessionId], sessions.Sessions.Select(x => x.Id));
    }

    private static ChangePasswordCommand Command(string currentPassword, string newPassword)
    {
        return new ChangePasswordCommand
        {
            UserId = UserId,
            SessionId = ThisSessionId,
            CurrentPassword = currentPassword,
            NewPassword = newPassword,
        };
    }

    private static (ChangePasswordCommandHandler, FakeUsersRepository, FakeSessionsRepository) Build(bool hasPassword)
    {
        var user = new User
        {
            Id = UserId,
            Username = "someone",
            Email = null,
            EmailVerified = false,
            HashedPassword = hasPassword ? new PasswordHasher<string>().HashPassword(UserId, CurrentPassword) : null,
            GoogleId = hasPassword ? null : "google-1",
            Created = DateTime.UnixEpoch,
            Updated = DateTime.UnixEpoch,
        };

        var users = new FakeUsersRepository(user);

        var sessions = new FakeSessionsRepository(
        [
            Session(ThisSessionId),
            Session(OtherSessionId),
        ]);

        return (new ChangePasswordCommandHandler(users, sessions, new ValidationService()), users, sessions);
    }

    private static Session Session(string id)
    {
        return new Session
        {
            Id = id,
            UserId = UserId,
            RefreshToken = $"refresh-{id}",
            Created = DateTime.UnixEpoch,
            Updated = DateTime.UnixEpoch,
        };
    }

    private class FakeUsersRepository : IUsersRepository
    {
        private readonly User _user;

        public User? Updated { get; private set; }

        public FakeUsersRepository(User user) => _user = user;

        public Task<Maybe<User>> GetById(string id, CancellationToken ct)
        {
            return Task.FromResult(id == _user.Id ? _user : Maybe<User>.None);
        }

        public Task<Result> Update(User updatedEntity, CancellationToken ct)
        {
            Updated = updatedEntity;

            return Task.FromResult(Result.Success());
        }

        public Task<Maybe<User>> GetVerifiedByEmail(string email, CancellationToken ct) => throw new NotSupportedException();
        public Task<Maybe<User>> GetByUsername(string username, CancellationToken ct) => throw new NotSupportedException();
        public Task<Maybe<User>> GetByGoogleId(string googleId, CancellationToken ct) => throw new NotSupportedException();
        public Task<List<User>> SearchByUsername(string keyword, UserIdScope scope, int skip, int pageSize, CancellationToken ct) => throw new NotSupportedException();
        public Task<List<User>> GetLatestUsers(UserIdScope scope, int skip, int pageSize, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> AnyWithUsername(string username, CancellationToken ct) => throw new NotSupportedException();
        public Task<IList<User>> GetByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> Insert(User entity, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> InsertMany(IEnumerable<User> entities, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> Upsert(User entity, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> Delete(string id, CancellationToken ct) => throw new NotSupportedException();
    }

    private class FakeSessionsRepository : ISessionsRepository
    {
        public List<Session> Sessions { get; }

        public FakeSessionsRepository(List<Session> sessions) => Sessions = sessions;

        public Task<Result> DeleteByUserIdExceptSession(string userId, string sessionId, CancellationToken ct)
        {
            Sessions.RemoveAll(x => x.UserId == userId && x.Id != sessionId);

            return Task.FromResult(Result.Success());
        }

        public Task<Maybe<Session>> GetByRefreshToken(string refreshToken, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> DeleteByRefreshToken(string refreshToken, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> DeleteByUserId(string userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Maybe<Session>> GetById(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task<IList<Session>> GetByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> Insert(Session entity, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> InsertMany(IEnumerable<Session> entities, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> Upsert(Session entity, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> Delete(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> Update(Session updatedEntity, CancellationToken ct) => throw new NotSupportedException();
    }
}
