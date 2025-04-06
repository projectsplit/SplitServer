using SplitServer.Models;

namespace SplitServer.Repositories;

public interface IGroupsRepository : IRepositoryBase<Group>
{
    Task<List<Group>> GetByUserId(string userId, bool? isArchived, int pageSize, DateTime? maxCreated, CancellationToken ct);

    Task<List<Group>> GetAllByUserId(string userId, CancellationToken ct);
}