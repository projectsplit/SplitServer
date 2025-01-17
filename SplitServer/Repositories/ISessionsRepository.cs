using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface ISessionsRepository : IRepositoryBase<Session>
{
    Task<Maybe<Session>> GetByRefreshToken(string refreshToken, CancellationToken ct);
    
    Task<Maybe<Session>> GetByPreviousRefreshToken(string refreshToken, CancellationToken ct);
    
    Task<Result> DeleteByRefreshToken(string refreshToken, CancellationToken ct);
}