using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface ICalculatedWealthRepository : IRepositoryBase<CalculatedWealth>
{
    Task<Maybe<CalculatedWealth>> GetByUserId(string userId, CancellationToken ct);

    Task<Result> UpsertByUserId(CalculatedWealth calculatedWealth, CancellationToken ct);
}
