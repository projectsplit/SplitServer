using MongoDB.Driver;
using SplitServer.Models;
using SplitServer.Repositories.Mappers;

namespace SplitServer.Repositories.Implementations;

public class PlusSubscriptionsMongoDbRepository :
    MongoDbRepositoryBase<PlusSubscription, PlusSubscription>,
    IPlusSubscriptionsRepository
{
    public PlusSubscriptionsMongoDbRepository(IMongoConnection mongoConnection) :
        base(
            mongoConnection,
            "PlusSubscriptions",
            new PassThroughMapper<PlusSubscription>())
    {
    }

    public async Task<List<PlusSubscription>> GetByUserId(string userId, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.UserId, userId);

        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<List<PlusSubscription>> GetNotInStates(
        IReadOnlyCollection<string> endedStates,
        CancellationToken ct)
    {
        var filter = FilterBuilder.Nin(x => x.State, endedStates);

        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<bool> TryClaimRenewalReminder(string id, DateTime expiresAt, CancellationToken ct)
    {
        // Ne also matches a row where the field is null or missing, which is every subscription
        // that has never had a reminder.
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.Id, id),
            FilterBuilder.Ne(x => x.RenewalReminderSentFor, expiresAt));

        var update = UpdateBuilder.Set(x => x.RenewalReminderSentFor, expiresAt);

        var result = await Collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = false }, ct);

        return result.IsAcknowledged && result.ModifiedCount == 1;
    }

    public async Task EnsureIndexes(CancellationToken ct)
    {
        // Backs "does this person have Plus", which reads all of one user's rows after every change
        // to any of them, and the same read when their account is deleted.
        var byUserIndex = new CreateIndexModel<PlusSubscription>(
            Builders<PlusSubscription>.IndexKeys.Ascending(x => x.UserId),
            new CreateIndexOptions { Name = "UserId" });

        // Backs the worker's hourly walk, which is everything not in a finished state. Over time
        // most rows are finished, and this keeps them out of the scan.
        var byStateIndex = new CreateIndexModel<PlusSubscription>(
            Builders<PlusSubscription>.IndexKeys.Ascending(x => x.State),
            new CreateIndexOptions { Name = "State" });

        await Collection.Indexes.CreateManyAsync([byUserIndex, byStateIndex], ct);
    }
}
