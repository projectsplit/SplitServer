using MongoDB.Driver;
using SplitServer.Models;
using SplitServer.Repositories.Mappers;

namespace SplitServer.Repositories.Implementations;

public class DonationSubscriptionsMongoDbRepository :
    MongoDbRepositoryBase<DonationSubscription, DonationSubscription>,
    IDonationSubscriptionsRepository
{
    public DonationSubscriptionsMongoDbRepository(IMongoConnection mongoConnection) :
        base(
            mongoConnection,
            "DonationSubscriptions",
            new PassThroughMapper<DonationSubscription>())
    {
    }

    public async Task<bool> HasActiveByUserId(string userId, CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.UserId, userId),
            FilterBuilder.Eq(x => x.IsActive, true));

        return await Collection.Find(filter).AnyAsync(ct);
    }

    public async Task<List<DonationSubscription>> GetByUserId(string userId, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.UserId, userId);

        return await Collection.Find(filter).ToListAsync(ct);
    }
}
