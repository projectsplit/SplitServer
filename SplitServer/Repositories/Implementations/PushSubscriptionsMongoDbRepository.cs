using CSharpFunctionalExtensions;
using MongoDB.Driver;
using SplitServer.Models;
using SplitServer.Repositories.Mappers;

namespace SplitServer.Repositories.Implementations;

public class PushSubscriptionsMongoDbRepository :
    MongoDbRepositoryBase<PushSubscription, PushSubscription>,
    IPushSubscriptionsRepository
{
    public PushSubscriptionsMongoDbRepository(IMongoConnection mongoConnection) :
        base(
            mongoConnection,
            "PushSubscriptions",
            new PassThroughMapper<PushSubscription>())
    {
    }

    public async Task<List<PushSubscription>> GetAllByUserIds(IList<string> userIds, CancellationToken ct)
    {
        var filter = FilterBuilder.In(x => x.UserId, userIds);

        return await Collection
            .Find(filter)
            .ToListAsync(ct);
    }

    public async Task<Result> DeleteByEndpoint(string endpoint, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.Endpoint, endpoint);

        var result = await Collection.DeleteManyAsync(filter, ct);

        return result.IsAcknowledged
            ? Result.Success()
            : Result.Failure("Failed to delete push subscription by endpoint");
    }
}
