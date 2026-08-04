using MongoDB.Driver;
using SplitServer.Models;
using SplitServer.Repositories.Mappers;

namespace SplitServer.Repositories.Implementations;

public class NotificationsMongoDbRepository :
    MongoDbRepositoryBase<Notification, Notification>,
    INotificationsRepository
{
    public NotificationsMongoDbRepository(IMongoConnection mongoConnection) :
        base(
            mongoConnection,
            "Notifications",
            new PassThroughMapper<Notification>())
    {
    }

    public async Task<List<Notification>> GetByUserId(
        string userId,
        int pageSize,
        DateTime maxCreatedDate,
        CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.UserId, userId),
            FilterBuilder.Lt(x => x.Created, maxCreatedDate));

        return await Collection
            .Find(filter)
            .Limit(pageSize)
            .SortByDescending(x => x.Created)
            .ToListAsync(ct);
    }

    /// <summary>
    /// How long a notification stays in the feed. This is an activity log, not a record anyone
    /// needs indefinitely, and without expiry the collection grows for as long as the app runs.
    /// </summary>
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(60);

    public async Task EnsureIndexes(CancellationToken ct)
    {
        // Mongo expires documents whose Created is older than the retention period, sweeping the
        // collection in the background so nothing has to prune it on the write path.
        var expiryIndex = new CreateIndexModel<Notification>(
            Builders<Notification>.IndexKeys.Ascending(x => x.Created),
            new CreateIndexOptions { ExpireAfter = RetentionPeriod, Name = "Created_ttl" });

        // Backs both feed queries: newest-first by user, and the unread count.
        var userFeedIndex = new CreateIndexModel<Notification>(
            Builders<Notification>.IndexKeys
                .Ascending(x => x.UserId)
                .Descending(x => x.Created),
            new CreateIndexOptions { Name = "UserId_Created" });

        await Collection.Indexes.CreateManyAsync([expiryIndex, userFeedIndex], ct);
    }

    public async Task<long> CountByUserIdAndMinCreated(string userId, DateTime minCreatedDate, CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.UserId, userId),
            FilterBuilder.Gt(x => x.Created, minCreatedDate));

        return await Collection.Find(filter).CountDocumentsAsync(ct);
    }
}
