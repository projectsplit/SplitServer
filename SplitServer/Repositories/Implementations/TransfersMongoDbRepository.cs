using CSharpFunctionalExtensions;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
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
        var filter = FilterBuilder.Eq(x => x.GroupId, groupId);

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

    public async Task<List<Transfer>> GetAllByMemberIds(List<string> memberIds, CancellationToken ct)
    {
        var receiverFilter = FilterBuilder.In(x => x.ReceiverId, memberIds);
        var senderFilter = FilterBuilder.In(x => x.SenderId, memberIds);

        var filter = FilterBuilder.And(
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

        return await Collection.Find(filter).AnyAsync(ct);
    }

    public async Task<List<Transfer>> Search(
        string groupId,
        string? searchTerm,
        DateTime? minTime,
        DateTime? maxTime,
        string[]? receiverIds,
        string[]? senderIds,
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

        var receiversFilter = !receiverIds.IsNullOrEmpty()
            ? FilterBuilder.In(x => x.ReceiverId, receiverIds)
            : FilterBuilder.Empty;

        var sendersFilter = !senderIds.IsNullOrEmpty()
            ? FilterBuilder.In(x => x.SenderId, senderIds)
            : FilterBuilder.Empty;

        var minTimeFilter = minTime is not null
            ? FilterBuilder.Gte(x => x.Occurred, minTime)
            : FilterBuilder.Empty;

        var maxTimeFilter = maxTime is not null
            ? FilterBuilder.Lte(x => x.Occurred, maxTime)
            : FilterBuilder.Empty;

        var descriptionFilter = searchTerm is not null
            ? FilterBuilder.Regex(x => x.Description, new BsonRegularExpression(searchTerm, "i"))
            : FilterBuilder.Empty;

        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.GroupId, groupId),
            receiversFilter,
            sendersFilter,
            minTimeFilter,
            maxTimeFilter,
            descriptionFilter,
            paginationFilter);

        var sort = SortBuilder.Descending(x => x.Occurred).Descending(x => x.Created);

        return await Collection
            .Find(filter)
            .Sort(sort)
            .Limit(pageSize)
            .ToListAsync(ct);
    }
}