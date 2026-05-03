using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface IRiskEngineRepository : IRepositoryBase<RiskEngineSetup>
{
    Task<Maybe<RiskEngineSetup>> GetByUserId(string userId, CancellationToken ct);

    Task<Result> UpsertByUserId(RiskEngineSetup setup, CancellationToken ct);
}
