using MongoDB.Driver;
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

    public async Task<List<Group>> GetByUserId(string userId, int pageSize, DateTime? maxCreated, CancellationToken ct)
    {
        var paginationFilter = maxCreated is not null
            ? FilterBuilder.Lt(x => x.Created, maxCreated)
            : FilterBuilder.Empty;

        var filter = FilterBuilder.And(
            FilterBuilder.ElemMatch(x => x.Members, x => x.UserId == userId),
            FilterBuilder.Eq(x => x.IsDeleted, false),
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
        var filter = FilterBuilder.And(
            FilterBuilder.Eq("Members.UserId", userId),
            FilterBuilder.Eq(x => x.IsDeleted, false));

        return await Collection
            .Find(filter)
            .ToListAsync(ct);
    }
}