using CSharpFunctionalExtensions;
using MongoDB.Driver;
using SplitServer.Models;
using SplitServer.Repositories.Mappers;

namespace SplitServer.Repositories.Implementations;

public class JoinTokensMongoDbRepository : MongoDbRepositoryBase<JoinToken, JoinToken>, IJoinTokensRepository
{
    public JoinTokensMongoDbRepository(IMongoConnection mongoConnection) :
        base(
            mongoConnection,
            "JoinTokens",
            new PassThroughMapper<JoinToken>())
    {
    }

    public async Task<List<JoinToken>> GetByGroupId(string groupId, int pageSize, DateTime? maxCreated, CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.GroupId, groupId),
            maxCreated is not null
                ? FilterBuilder.Lt(x => x.Created, maxCreated)
                : FilterBuilder.Empty);

        return await Collection
            .Find(filter)
            .SortByDescending(x => x.Created)
            .Limit(pageSize)
            .ToListAsync(ct);
    }

    public async Task<Result> DeleteByGroupId(string groupId, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.GroupId, groupId);
        var result = await Collection.DeleteManyAsync(filter, ct);

        return result.IsAcknowledged ? Result.Success() : Result.Failure("Failed to delete group join tokens");
    }
}