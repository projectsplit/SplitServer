using SplitServer.Models;

namespace SplitServer.Repositories;

public interface IGroupsRepository : IRepositoryBase<Group>
{
    Task<List<Group>> GetByUserId(string userId, bool? isArchived, int pageSize, DateTime? maxCreated, CancellationToken ct);

    /// <summary>
    /// Groups the user is currently a member of. A group they have left is not one of them, and
    /// that is deliberate: its expenses stop counting towards them everywhere, until they rejoin by
    /// reclaiming the guest slot that holds their old member id.
    /// </summary>
    Task<List<Group>> GetAllByUserId(string userId, CancellationToken ct);

    Task<List<Group>> SearchByGroupName(string userId, string keyword, int skip, int pageSize, CancellationToken ct);
    
}