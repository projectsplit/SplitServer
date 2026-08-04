using SplitServer.Models;

namespace SplitServer.Repositories;

public interface INotificationsRepository : IRepositoryBase<Notification>
{
    Task<List<Notification>> GetByUserId(string userId, int pageSize, DateTime maxCreatedDate, CancellationToken ct);

    Task<long> CountByUserIdAndMinCreated(string userId, DateTime minCreatedDate, CancellationToken ct);

    /// <summary>
    /// Creates the TTL index that expires old notifications. Called once at startup; Mongo treats
    /// re-creating an identical index as a no-op.
    /// </summary>
    Task EnsureIndexes(CancellationToken ct);
}
