using CSharpFunctionalExtensions;
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

    /// <summary>
    /// Replaces the group only if nobody has written to it since it was read, identified by the
    /// Updated stamp on the copy that was read. False means a concurrent write got there first. A
    /// group is one document holding its members, guests and labels together, so a plain replace
    /// from a stale copy silently undoes whatever landed in between — a member who joined, a guest
    /// who was replaced — and every handler that rewrites a group goes through this instead.
    /// </summary>
    Task<Result<bool>> UpdateIfUnchanged(Group entity, DateTime expectedUpdated, CancellationToken ct);
}