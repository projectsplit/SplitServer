using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface IJoinCodesRepository : IRepositoryBase<JoinCode>
{
    Task<List<JoinCode>> GetByGroupId(string groupId, int pageSize, DateTime? maxCreated, CancellationToken ct);

    Task<Result> DeleteByGroupId(string groupId, CancellationToken ct);
}