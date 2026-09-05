using CSharpFunctionalExtensions;
using MongoDB.Driver;
using SplitServer.Models;
using SplitServer.Repositories.Mappers;

namespace SplitServer.Repositories.Implementations;

public class DonationsMongoDbRepository :
    MongoDbRepositoryBase<Donation, Donation>,
    IDonationsRepository
{
    public DonationsMongoDbRepository(IMongoConnection mongoConnection) :
        base(
            mongoConnection,
            "Donations",
            new PassThroughMapper<Donation>())
    {
    }

    public async Task<List<Donation>> GetByUserId(string userId, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.UserId, userId);

        return await Collection
            .Find(filter)
            .SortByDescending(x => x.Created)
            .ToListAsync(ct);
    }

    public async Task<Maybe<Donation>> GetByPurchaseToken(string purchaseToken, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.PurchaseToken, purchaseToken);

        // A subscription's renewals all share one token, so the newest row is the one a refund is
        // about; for a one-off there is only ever the one.
        var donation = await Collection
            .Find(filter)
            .SortByDescending(x => x.Created)
            .FirstOrDefaultAsync(ct);

        return donation ?? Maybe<Donation>.None;
    }
}
