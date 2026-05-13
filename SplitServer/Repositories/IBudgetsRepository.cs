using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface IBudgetsRepository : IRepositoryBase<Budget>
{
    Task<List<Budget>> GetAllByUserId(string userId, CancellationToken ct);

    Task<Result> DeactivateAllByUserId(string userId, CancellationToken ct);
}