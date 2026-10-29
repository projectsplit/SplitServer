using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface ISessionsRepository : IRepositoryBase<Session>
{
    Task<Maybe<Session>> GetByRefreshToken(string refreshToken, CancellationToken ct);

    Task<Result> DeleteByRefreshToken(string refreshToken, CancellationToken ct);

    Task<Result> DeleteByUserId(string userId, CancellationToken ct);

    /// <summary>
    /// Signs the user out of every session but the given one.
    /// </summary>
    Task<Result> DeleteByUserIdExceptSession(string userId, string sessionId, CancellationToken ct);
}