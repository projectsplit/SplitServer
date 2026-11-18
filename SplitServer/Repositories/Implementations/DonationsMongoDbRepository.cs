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

    public async Task<Result> SetCharged(string id, decimal amount, string currency, CancellationToken ct)
    {
        var update = UpdateBuilder
            .Set(x => x.ChargedAmount, amount)
            .Set(x => x.ChargedCurrency, currency);

        var result = await Collection.UpdateOneAsync(
            FilterBuilder.Eq(x => x.Id, id),
            update,
            new UpdateOptions { IsUpsert = false },
            ct);

        return result.IsAcknowledged ? Result.Success() : Result.Failure("Setting the charged amount failed");
    }

    public async Task<List<string>> GetDonorUserIds(CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.Status, DonationStatus.Succeeded);

        var userIds = await (await Collection.DistinctAsync(x => x.UserId, filter, cancellationToken: ct))
            .ToListAsync(ct);

        // A row can be left without a user on paper, and there is nobody to summarise it onto.
        return userIds.Where(x => !string.IsNullOrEmpty(x)).Select(x => x!).ToList();
    }
}
