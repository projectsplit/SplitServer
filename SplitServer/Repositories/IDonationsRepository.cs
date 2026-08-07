using SplitServer.Models;

namespace SplitServer.Repositories;

public interface IDonationsRepository : IRepositoryBase<Donation>
{
    Task<List<Donation>> GetByUserId(string userId, CancellationToken ct);
}
