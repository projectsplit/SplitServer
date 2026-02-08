using CSharpFunctionalExtensions;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using SplitServer.Models;
using SplitServer.Repositories.Implementations.Models;
using SplitServer.Repositories.Mappers;

namespace SplitServer.Repositories.Implementations;

public class TransfersMongoDbRepository : MongoDbRepositoryBase<Transfer, TransferMongoDbDocument>, ITransfersRepository
{
    private readonly IMongoCollection<GroupTransferMongoDbDocument> _groupTransfersCollection;
    private readonly IMongoCollection<NonGroupTransferMongoDbDocument> _nonGroupTransfersCollection;

    public TransfersMongoDbRepository(IMongoConnection mongoConnection) :
        base(
            mongoConnection,
            "Transfers",
            new TransferMapper())
    {
        _groupTransfersCollection = Collection.Database
            .GetCollection<GroupTransferMongoDbDocument>(Collection.CollectionNamespace.CollectionName);

        _nonGroupTransfersCollection = Collection.Database
            .GetCollection<NonGroupTransferMongoDbDocument>(Collection.CollectionNamespace.CollectionName);
    }

    public async Task<List<GroupTransfer>> GetByGroupId(
        string groupId,
        int pageSize,
        DateTime? maxOccurred,
        DateTime? maxCreated,
        CancellationToken ct)
    {
        var filterBuilder = Builders<GroupTransferMongoDbDocument>.Filter;
        var sortBuilder = Builders<GroupTransferMongoDbDocument>.Sort;

        var paginationFilter = maxOccurred is not null && maxCreated is not null
            ? filterBuilder.Or(
                filterBuilder.Lt(x => x.Occurred, maxOccurred),
                filterBuilder.And(
                    filterBuilder.Eq(x => x.Occurred, maxOccurred),
                    filterBuilder.Lt(x => x.Created, maxCreated)))
            : filterBuilder.Empty;

        var filter = filterBuilder.And(
            filterBuilder.Eq(x => x.GroupId, groupId),
            paginationFilter);

        var sort = sortBuilder.Descending(x => x.Occurred).Descending(x => x.Created);

        var documents = await _groupTransfersCollection
            .Find(filter)
            .Sort(sort)
            .Limit(pageSize)
            .ToListAsync(ct);

        return documents.Select(d => (GroupTransfer)Mapper.ToEntity(d)).ToList();
    }

    public async Task<List<NonGroupTransfer>> GetByUserId(
        string userId,
        int pageSize,
        DateTime? maxOccurred,
        DateTime? maxCreated,
        CancellationToken ct)
    {
        var filterBuilder = Builders<NonGroupTransferMongoDbDocument>.Filter;
        var sortBuilder = Builders<NonGroupTransferMongoDbDocument>.Sort;

        var paginationFilter = maxOccurred is not null && maxCreated is not null
            ? filterBuilder.Or(
                filterBuilder.Lt(x => x.Occurred, maxOccurred),
                filterBuilder.And(
                    filterBuilder.Eq(x => x.Occurred, maxOccurred),
                    filterBuilder.Lt(x => x.Created, maxCreated)))
            : filterBuilder.Empty;

        var filter = filterBuilder.And(
            filterBuilder.Or(
                filterBuilder.Eq(x => x.SenderId, userId),
                filterBuilder.Eq(x => x.ReceiverId, userId)
            ),
            paginationFilter);

        var sort = sortBuilder.Descending(x => x.Occurred).Descending(x => x.Created);

        var documents = await _nonGroupTransfersCollection
            .Find(filter)
            .Sort(sort)
            .Limit(pageSize)
            .ToListAsync(ct);

        return documents.Select(d => (NonGroupTransfer)Mapper.ToEntity(d)).ToList();
    }

    public async Task<List<GroupTransfer>> GetAllByGroupId(string groupId, CancellationToken ct)
    {
        var filterBuilder = Builders<GroupTransferMongoDbDocument>.Filter;
        var filter = filterBuilder.Eq(x => x.GroupId, groupId);

        var documents = await _groupTransfersCollection
            .Find(filter)
            .ToListAsync(ct);

        return documents.Select(d => (GroupTransfer)Mapper.ToEntity(d)).ToList();
    }

    public async Task<Result> DeleteByGroupId(string groupId, CancellationToken ct)
    {
        var filterBuilder = Builders<GroupTransferMongoDbDocument>.Filter;
        var filter = filterBuilder.Eq(x => x.GroupId, groupId);

        var result = await _groupTransfersCollection.DeleteManyAsync(filter, null, ct);

        return result.IsAcknowledged ? Result.Success() : Result.Failure("Failed to delete group transfers");
    }

    public async Task<List<GroupTransfer>> GetAllByMemberIds(List<string> memberIds, CancellationToken ct)
    {
        var filterBuilder = Builders<GroupTransferMongoDbDocument>.Filter;

        var receiverFilter = filterBuilder.In(x => x.ReceiverId, memberIds);
        var senderFilter = filterBuilder.In(x => x.SenderId, memberIds);

        var filter = filterBuilder.And(
            filterBuilder.Or(receiverFilter, senderFilter));

        var documents = await _groupTransfersCollection
            .Find(filter)
            .ToListAsync(ct);

        return documents.Select(d => (GroupTransfer)Mapper.ToEntity(d)).ToList();
    }

    public async Task<bool> ExistsInAnyTransfer(string groupId, string memberId, CancellationToken ct)
    {
        var filterBuilder = Builders<GroupTransferMongoDbDocument>.Filter;

        var filter = filterBuilder.And(
            filterBuilder.Eq(x => x.GroupId, groupId),
            filterBuilder.Or(
                filterBuilder.Eq(x => x.ReceiverId, memberId),
                filterBuilder.Eq(x => x.SenderId, memberId)));

        return await _groupTransfersCollection.Find(filter).AnyAsync(ct);
    }

    public async Task<List<GroupTransfer>> Search(
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
        var filterBuilder = Builders<GroupTransferMongoDbDocument>.Filter;
        var sortBuilder = Builders<GroupTransferMongoDbDocument>.Sort;

        var paginationFilter = maxOccurred is not null && maxCreated is not null
            ? filterBuilder.Or(
                filterBuilder.Lt(x => x.Occurred, maxOccurred),
                filterBuilder.And(
                    filterBuilder.Eq(x => x.Occurred, maxOccurred),
                    filterBuilder.Lt(x => x.Created, maxCreated)))
            : filterBuilder.Empty;

        var receiversFilter = !receiverIds.IsNullOrEmpty()
            ? filterBuilder.In(x => x.ReceiverId, receiverIds)
            : filterBuilder.Empty;

        var sendersFilter = !senderIds.IsNullOrEmpty()
            ? filterBuilder.In(x => x.SenderId, senderIds)
            : filterBuilder.Empty;

        var minTimeFilter = minTime is not null
            ? filterBuilder.Gte(x => x.Occurred, minTime)
            : filterBuilder.Empty;

        var maxTimeFilter = maxTime is not null
            ? filterBuilder.Lte(x => x.Occurred, maxTime)
            : filterBuilder.Empty;

        var descriptionFilter = searchTerm is not null
            ? filterBuilder.Regex(x => x.Description, new BsonRegularExpression(searchTerm, "i"))
            : filterBuilder.Empty;

        var filter = filterBuilder.And(
            filterBuilder.Eq(x => x.GroupId, groupId),
            receiversFilter,
            sendersFilter,
            minTimeFilter,
            maxTimeFilter,
            descriptionFilter,
            paginationFilter);

        var sort = sortBuilder.Descending(x => x.Occurred).Descending(x => x.Created);

        var documents = await _groupTransfersCollection
            .Find(filter)
            .Sort(sort)
            .Limit(pageSize)
            .ToListAsync(ct);

        return documents.Select(d => (GroupTransfer)Mapper.ToEntity(d)).ToList();
    }

    public async Task<List<NonGroupTransfer>> SearchNonGroup(
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
        var filterBuilder = Builders<NonGroupTransferMongoDbDocument>.Filter;
        var sortBuilder = Builders<NonGroupTransferMongoDbDocument>.Sort;

        var paginationFilter = maxOccurred is not null && maxCreated is not null
            ? filterBuilder.Or(
                filterBuilder.Lt(x => x.Occurred, maxOccurred),
                filterBuilder.And(
                    filterBuilder.Eq(x => x.Occurred, maxOccurred),
                    filterBuilder.Lt(x => x.Created, maxCreated)))
            : filterBuilder.Empty;

        var receiversFilter = !receiverIds.IsNullOrEmpty()
            ? filterBuilder.In(x => x.ReceiverId, receiverIds)
            : filterBuilder.Empty;

        var sendersFilter = !senderIds.IsNullOrEmpty()
            ? filterBuilder.In(x => x.SenderId, senderIds)
            : filterBuilder.Empty;

        var minTimeFilter = minTime is not null
            ? filterBuilder.Gte(x => x.Occurred, minTime)
            : filterBuilder.Empty;

        var maxTimeFilter = maxTime is not null
            ? filterBuilder.Lte(x => x.Occurred, maxTime)
            : filterBuilder.Empty;

        var descriptionFilter = searchTerm is not null
            ? filterBuilder.Regex(x => x.Description, new BsonRegularExpression(searchTerm, "i"))
            : filterBuilder.Empty;

        var filter = filterBuilder.And(
            receiversFilter,
            sendersFilter,
            minTimeFilter,
            maxTimeFilter,
            descriptionFilter,
            paginationFilter);

        var sort = sortBuilder.Descending(x => x.Occurred).Descending(x => x.Created);

        var documents = await _nonGroupTransfersCollection
            .Find(filter)
            .Sort(sort)
            .Limit(pageSize)
            .ToListAsync(ct);

        return documents.Select(d => (NonGroupTransfer)Mapper.ToEntity(d)).ToList();
    }

    public async Task<List<string>> GetNonGroupUserIdsByUserId(string userId, CancellationToken ct)
    {
        var filterBuilder = Builders<NonGroupTransferMongoDbDocument>.Filter;
        var sortBuilder = Builders<NonGroupTransferMongoDbDocument>.Sort;

        var filter = filterBuilder.Or(
            filterBuilder.Eq(x => x.SenderId, userId),
            filterBuilder.Eq(x => x.ReceiverId, userId));

        var sort = sortBuilder.Descending(x => x.Created);

        var documents = await _nonGroupTransfersCollection
            .Find(filter)
            .Sort(sort)
            .ToListAsync(ct);

        var senderIds = documents.Select(x => x.SenderId);
        var receiverIds = documents.Select(x => x.ReceiverId);

        return senderIds.Concat(receiverIds)
            .Distinct()
            .ToList();
    }
}