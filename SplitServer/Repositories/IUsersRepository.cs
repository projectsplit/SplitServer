using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface IUsersRepository : IRepositoryBase<User>
{
    /// <summary>
    /// Returns the account that owns this email, i.e. the one that has verified it.
    /// An unverified email can be held by several accounts at once, so it never identifies an owner.
    /// </summary>
    Task<Maybe<User>> GetVerifiedByEmail(string email, CancellationToken ct);

    Task<Maybe<User>> GetByUsername(string username, CancellationToken ct);

    Task<Maybe<User>> GetByGoogleId(string googleId, CancellationToken ct);

    Task<List<User>> SearchByUsername(string keyword, UserIdScope scope, int skip, int pageSize, CancellationToken ct);

    Task<List<User>> GetLatestUsers(UserIdScope scope, int skip, int pageSize, CancellationToken ct);

    Task<bool> AnyWithUsername(string username, CancellationToken ct);
}