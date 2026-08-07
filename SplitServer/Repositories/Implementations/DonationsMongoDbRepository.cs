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
}
