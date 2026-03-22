using SplitServer.Models;

namespace SplitServer.Repositories;

public interface IUserLabelsRepository : IRepositoryBase<UserLabel>
{
    Task<List<UserLabel>> GetByUserId(string userId, CancellationToken ct);
}