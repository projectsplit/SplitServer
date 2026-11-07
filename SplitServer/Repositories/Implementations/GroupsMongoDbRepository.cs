using CSharpFunctionalExtensions;
using MongoDB.Driver;
using MongoDB.Driver.Search;
using SplitServer.Models;
using SplitServer.Repositories.Mappers;

namespace SplitServer.Repositories.Implementations;

public class GroupsMongoDbRepository : MongoDbRepositoryBase<Group, Group>, IGroupsRepository
{
    public GroupsMongoDbRepository(IMongoConnection mongoConnection) :
        base(
            mongoConnection,
            "Groups",
            new PassThroughMapper<Group>())
    {
    }

    public async Task<List<Group>> GetByUserId(string userId, bool? isArchived, int pageSize, DateTime? maxCreated, CancellationToken ct)
    {
        var archiveFilter = isArchived is not null
            ? FilterBuilder.Eq(x => x.IsArchived, isArchived)
            : FilterBuilder.Empty;

        var paginationFilter = maxCreated is not null
            ? FilterBuilder.Lt(x => x.Created, maxCreated)
            : FilterBuilder.Empty;

        var filter = FilterBuilder.And(
            FilterBuilder.ElemMatch(x => x.Members, x => x.UserId == userId),
            archiveFilter,
            paginationFilter);

        var sort = SortBuilder.Descending(x => x.Created);

        return await Collection
            .Find(filter)
            .Sort(sort)
            .Limit(pageSize)
            .ToListAsync(ct);
    }

    public async Task<List<Group>> GetAllByUserId(string userId, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq("Members.UserId", userId);

        return await Collection
            .Find(filter)
            .ToListAsync(ct);
    }

    public async Task<List<Group>> SearchByGroupName(string userId, string keyword, int skip, int pageSize, CancellationToken ct)
    {
        var search = SearchBuilder.Autocomplete(
            x => x.Name,
            new SingleSearchQueryDefinition(keyword),
            fuzzy: new SearchFuzzyOptions { MaxEdits = 1, PrefixLength = 4 });

        var filter = FilterBuilder.And(
            FilterBuilder.ElemMatch(x => x.Members, x => x.UserId == userId));

        var pipelineDefinition = PipelineBuilder
            .Search(search)
            .Match(filter)
            .Skip(skip)
            .Limit(pageSize);

        return await Collection
            .Aggregate(pipelineDefinition, cancellationToken: ct)
            .ToListAsync(ct);
    }

    public async Task<Result<bool>> UpdateIfUnchanged(Group entity, DateTime expectedUpdated, CancellationToken ct)
    {
        // The stamp compared here always comes from a document read back out of Mongo, so both
        // sides carry BSON's millisecond precision and equality is exact.
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.Id, entity.Id),
            FilterBuilder.Eq(x => x.Updated, expectedUpdated));

        var result = await Collection.ReplaceOneAsync(
            filter,
            Mapper.ToDocument(entity),
            new ReplaceOptions { IsUpsert = false },
            ct);

        return result.IsAcknowledged
            ? Result.Success(result.MatchedCount > 0)
            : Result.Failure<bool>("Update failed");
    }
}