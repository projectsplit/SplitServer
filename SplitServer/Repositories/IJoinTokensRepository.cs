using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface IJoinTokensRepository : IRepositoryBase<JoinToken>
{
    Task<List<JoinToken>> GetByGroupId(string groupId, int pageSize, DateTime? maxCreated, CancellationToken ct);

    Task<Result> DeleteByGroupId(string groupId, CancellationToken ct);
}