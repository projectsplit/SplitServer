using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface IUsersRepository : IRepositoryBase<User>
{
    Task<Maybe<User>> GetByEmail(string email, CancellationToken ct);

    Task<Maybe<User>> GetByUsername(string username, CancellationToken ct);

    Task<Maybe<User>> GetByGoogleId(string googleId, CancellationToken ct);

    Task<List<User>> SearchByUsername(string keyword, int skip, int pageSize, CancellationToken ct);

    Task<List<User>> GetLatestUsers(int skip, int pageSize, CancellationToken ct);

    Task<bool> AnyWithUsername(string username, CancellationToken ct);
}