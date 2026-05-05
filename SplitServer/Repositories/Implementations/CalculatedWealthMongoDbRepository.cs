using CSharpFunctionalExtensions;
using MongoDB.Driver;
using SplitServer.Models;
using SplitServer.Repositories.Mappers;

namespace SplitServer.Repositories.Implementations;

public class CalculatedWealthMongoDbRepository : MongoDbRepositoryBase<CalculatedWealth, CalculatedWealth>, ICalculatedWealthRepository
{
    public CalculatedWealthMongoDbRepository(IMongoConnection mongoConnection) :
        base(
            mongoConnection,
            "CalculatedWealth",
            new PassThroughMapper<CalculatedWealth>())
    {
    }

    public async Task<Maybe<CalculatedWealth>> GetByUserId(string userId, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.UserId, userId);

        var document = await Collection
            .Find(filter)
            .SingleOrDefaultAsync(ct);

        return document is not null
            ? Mapper.ToEntity(document)
            : Maybe<CalculatedWealth>.None;
    }

    public async Task<Result> UpsertByUserId(CalculatedWealth calculatedWealth, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.UserId, calculatedWealth.UserId);

        var result = await Collection.ReplaceOneAsync(
            filter,
            Mapper.ToDocument(calculatedWealth),
            new ReplaceOptions { IsUpsert = true },
            ct);

        return result.IsAcknowledged ? Result.Success() : Result.Failure("Upsert failed");
    }
}
