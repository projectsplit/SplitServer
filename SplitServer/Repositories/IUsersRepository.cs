using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface IUsersRepository : IRepositoryBase<User>
{
    Task<Maybe<User>> GetByEmail(string email, CancellationToken ct);
    
    Task<Maybe<User>> GetByUsername(string username, CancellationToken ct);
    
    Task<Maybe<User>> GetByGoogleId(string googleId, CancellationToken ct);
}