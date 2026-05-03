using CSharpFunctionalExtensions;
using MongoDB.Driver;
using SplitServer.Models;
using SplitServer.Repositories.Mappers;

namespace SplitServer.Repositories.Implementations;

public class RiskEngineMongoDbRepository : MongoDbRepositoryBase<RiskEngineSetup, RiskEngineSetup>, IRiskEngineRepository
{
    public RiskEngineMongoDbRepository(IMongoConnection mongoConnection) :
        base(
            mongoConnection,
            "RiskEngineSetups",
            new PassThroughMapper<RiskEngineSetup>())
    {
    }

    public async Task<Maybe<RiskEngineSetup>> GetByUserId(string userId, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.UserId, userId);

        var document = await Collection
            .Find(filter)
            .SingleOrDefaultAsync(ct);

        return document is not null
            ? Mapper.ToEntity(document)
            : Maybe<RiskEngineSetup>.None;
    }

    public async Task<Result> UpsertByUserId(RiskEngineSetup setup, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.UserId, setup.UserId);

        var result = await Collection.ReplaceOneAsync(
            filter,
            Mapper.ToDocument(setup),
            new ReplaceOptions { IsUpsert = true },
            ct);

        return result.IsAcknowledged ? Result.Success() : Result.Failure("Upsert failed");
    }
}
