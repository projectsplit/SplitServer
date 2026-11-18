using CSharpFunctionalExtensions;
using MongoDB.Driver;
using SplitServer.Models;
using SplitServer.Repositories.Mappers;

namespace SplitServer.Repositories.Implementations;

public class PlusStateMongoDbRepository :
    MongoDbRepositoryBase<PlusLaunch, PlusLaunch>,
    IPlusStateRepository
{
    public PlusStateMongoDbRepository(IMongoConnection mongoConnection) :
        base(
            mongoConnection,
            "PlusState",
            new PassThroughMapper<PlusLaunch>())
    {
    }

    public Task<Maybe<PlusLaunch>> GetLaunch(CancellationToken ct) => GetById(PlusLaunch.DocumentId, ct);

    public async Task<Result> InsertLaunchIfAbsent(PlusLaunch launch, CancellationToken ct)
    {
        // Every field under $setOnInsert, so an existing document matches the filter and is then
        // changed in no way at all.
        var update = UpdateBuilder
            .SetOnInsert(x => x.Created, launch.Created)
            .SetOnInsert(x => x.Updated, launch.Updated)
            .SetOnInsert(x => x.LiveSince, launch.LiveSince);

        try
        {
            var result = await Collection.UpdateOneAsync(
                FilterBuilder.Eq(x => x.Id, launch.Id),
                update,
                new UpdateOptions { IsUpsert = true },
                ct);

            return result.IsAcknowledged ? Result.Success() : Result.Failure("Writing the Plus launch moment failed");
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Two upserts for the same id can race, and the loser is told the key exists. That is
            // the outcome being asked for: the document is there, and it was not this call's.
            return Result.Success();
        }
    }
}
