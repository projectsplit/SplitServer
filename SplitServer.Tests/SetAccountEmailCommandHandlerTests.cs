using CSharpFunctionalExtensions;
using SplitServer.Commands;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;
using SplitServer.Services.Email;

namespace SplitServer.Tests;

/// <summary>
/// A code is mailed only when there is something to verify: a new address, or one not yet proven.
/// An address this account has already verified gets nothing, however often it is submitted.
/// </summary>
public class SetAccountEmailCommandHandlerTests
{
    private const string UserId = "user-1";
    private const string CurrentEmail = "someone@example.com";
    private const string NewEmail = "someone.else@example.com";

    [Fact]
    public async Task Verified_address_submitted_again_sends_nothing()
    {
        var (handler, users, codes, sender) = Build(emailVerified: true);

        var result = await handler.Handle(Command("Someone@Example.com"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(SetAccountEmailCommandHandler.AlreadyVerifiedError, result.Error);
        Assert.Empty(sender.Sent);
        Assert.Empty(codes.Inserted);
        Assert.Null(users.Updated);
    }

    [Fact]
    public async Task Unverified_address_submitted_again_gets_a_fresh_code()
    {
        var (handler, users, codes, sender) = Build(emailVerified: false);

        var result = await handler.Handle(Command(CurrentEmail), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : string.Empty);
        Assert.Equal([CurrentEmail], sender.Sent);
        Assert.Single(codes.Inserted);
        Assert.False(users.Updated?.EmailVerified);
    }

    [Fact]
    public async Task New_address_gets_a_code_and_starts_unverified()
    {
        var (handler, users, codes, sender) = Build(emailVerified: true);

        var result = await handler.Handle(Command(NewEmail), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : string.Empty);
        Assert.Equal([NewEmail], sender.Sent);
        Assert.Single(codes.Inserted);
        Assert.Equal(NewEmail, users.Updated?.Email);
        Assert.False(users.Updated?.EmailVerified);
    }

    private static SetAccountEmailCommand Command(string email)
    {
        return new SetAccountEmailCommand
        {
            UserId = UserId,
            Email = email,
        };
    }

    private static (SetAccountEmailCommandHandler, FakeUsersRepository, FakeCodesRepository, FakeEmailSender) Build(
        bool emailVerified)
    {
        var user = new User
        {
            Id = UserId,
            Username = "someone",
            Email = CurrentEmail,
            EmailVerified = emailVerified,
            HashedPassword = null,
            GoogleId = null,
            Created = DateTime.UnixEpoch,
            Updated = DateTime.UnixEpoch,
        };

        var users = new FakeUsersRepository(user);
        var codes = new FakeCodesRepository();
        var sender = new FakeEmailSender();

        var handler = new SetAccountEmailCommandHandler(
            users,
            codes,
            sender,
            new EmailTokenService(),
            new ValidationService());

        return (handler, users, codes, sender);
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

        public Task<Maybe<User>> GetVerifiedByEmail(string email, CancellationToken ct)
        {
            return Task.FromResult(Maybe<User>.None);
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
        public Task<bool> AnyWithUsername(string username, CancellationToken ct) => throw new NotSupportedException();
        public Task<IList<User>> GetByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> Insert(User entity, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> InsertMany(IEnumerable<User> entities, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> Upsert(User entity, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> Delete(string id, CancellationToken ct) => throw new NotSupportedException();
    }

    private class FakeCodesRepository : IEmailVerificationCodesRepository
    {
        public List<EmailVerificationCode> Inserted { get; } = [];

        public Task<Result> InvalidateActiveCodes(string userId, EmailVerificationCodePurpose purpose, CancellationToken ct)
        {
            return Task.FromResult(Result.Success());
        }

        public Task<Result> Insert(EmailVerificationCode entity, CancellationToken ct)
        {
            Inserted.Add(entity);

            return Task.FromResult(Result.Success());
        }

        public Task<Maybe<EmailVerificationCode>> GetActiveByCodeHash(string codeHash, EmailVerificationCodePurpose purpose, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> DeleteByUserId(string userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Maybe<EmailVerificationCode>> GetById(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task<IList<EmailVerificationCode>> GetByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> InsertMany(IEnumerable<EmailVerificationCode> entities, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> Upsert(EmailVerificationCode entity, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> Delete(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> Update(EmailVerificationCode updatedEntity, CancellationToken ct) => throw new NotSupportedException();
    }

    private class FakeEmailSender : IEmailSender
    {
        public List<string> Sent { get; } = [];

        public Task<Result> SendAsync(string toAddress, string subject, string body, CancellationToken ct)
        {
            Sent.Add(toAddress);

            return Task.FromResult(Result.Success());
        }
    }
}
