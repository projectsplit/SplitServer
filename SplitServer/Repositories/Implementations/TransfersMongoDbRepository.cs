using CSharpFunctionalExtensions;
using MongoDB.Driver;
using SplitServer.Models;
using SplitServer.Repositories.Mappers;

namespace SplitServer.Repositories.Implementations;

public class TransfersMongoDbRepository : MongoDbRepositoryBase<Transfer, Transfer>, ITransfersRepository
{
    public TransfersMongoDbRepository(IMongoConnection mongoConnection) :
        base(
            mongoConnection,
            "Transfers",
            new PassThroughMapper<Transfer>())
    {
    }

    public async Task<List<Transfer>> GetByGroupId(
        string groupId,
        int pageSize,
        DateTime? maxOccurred,
        DateTime? maxCreated,
        CancellationToken ct)
    {
        var paginationFilter = maxOccurred is not null && maxCreated is not null
            ? FilterBuilder.Or(
                FilterBuilder.Lt(x => x.Occurred, maxOccurred),
                FilterBuilder.And(
                    FilterBuilder.Eq(x => x.Occurred, maxOccurred),
                    FilterBuilder.Lt(x => x.Created, maxCreated)))
            : FilterBuilder.Empty;

        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.GroupId, groupId),
            FilterBuilder.Eq(x => x.IsDeleted, false),
            paginationFilter);

        var sort = SortBuilder.Descending(x => x.Occurred).Descending(x => x.Created);

        return await Collection
            .Find(filter)
            .Sort(sort)
            .Limit(pageSize)
            .ToListAsync(ct);
    }

    public async Task<List<Transfer>> GetAllByGroupId(string groupId, CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.GroupId, groupId),
            FilterBuilder.Eq(x => x.IsDeleted, false));

        return await Collection
            .Find(filter)
            .ToListAsync(ct);
    }

    public async Task<Result> DeleteByGroupId(string groupId, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.GroupId, groupId);

        var result = await Collection.DeleteManyAsync(filter, null, ct);

        return result.IsAcknowledged ? Result.Success() : Result.Failure("Failed to delete group transfers");
    }

    public async Task<Result> SoftDeleteByGroupId(string groupId, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.GroupId, groupId);
        var update = UpdateBuilder.Set(x => x.IsDeleted, true);

        var result = await Collection.UpdateManyAsync(filter, update, null, ct);

        return result.IsAcknowledged ? Result.Success() : Result.Failure("Failed to delete group transfers");
    }

    public async Task<List<Transfer>> GetAllByMemberIds(List<string> memberIds, CancellationToken ct)
    {
        var receiverFilter = FilterBuilder.In(x => x.ReceiverId, memberIds);
        var senderFilter = FilterBuilder.In(x => x.SenderId, memberIds);

        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.IsDeleted, false),
            FilterBuilder.Or(receiverFilter, senderFilter));

        var documents = await Collection
            .Find(filter)
            .ToListAsync(ct);

        return documents.Select(Mapper.ToEntity).ToList();
    }

    public async Task<bool> ExistsInAnyTransfer(string groupId, string memberId, CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.GroupId, groupId),
            FilterBuilder.Or(
                FilterBuilder.Eq(x => x.ReceiverId, memberId),
                FilterBuilder.Eq(x => x.SenderId, memberId)));

        return await Collection.Find(filter).Limit(1).AnyAsync(ct);
    }
}