using CSharpFunctionalExtensions;
using MongoDB.Driver;
using SplitServer.Models;
using SplitServer.Repositories.Mappers;

namespace SplitServer.Repositories.Implementations;

public class InvitationsMongoDbRepository : MongoDbRepositoryBase<Invitation, Invitation>, IInvitationsRepository
{
    public InvitationsMongoDbRepository(IMongoConnection mongoConnection) :
        base(
            mongoConnection,
            "Invitations",
            new PassThroughMapper<Invitation>())
    {
    }

    public async Task<Maybe<Invitation>> Get(string senderId, string receiverId, string groupId, CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.SenderId, senderId),
            FilterBuilder.Eq(x => x.ReceiverId, receiverId),
            FilterBuilder.Eq(x => x.GroupId, groupId));

        return await Collection.Find(filter).SingleOrDefaultAsync(ct);
    }

    public async Task<List<Invitation>> GetByReceiverId(string receiverId, int pageSize, DateTime maxCreatedDate, CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.ReceiverId, receiverId),
            FilterBuilder.Lt(x => x.Created, maxCreatedDate));

        return await Collection
            .Find(filter)
            .Limit(pageSize)
            .SortByDescending(x => x.Created)
            .ToListAsync(ct);
    }

    public async Task<List<Invitation>> GetByReceiverIds(List<string> receiverIds, string groupId, CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.GroupId, groupId),
            FilterBuilder.In(x => x.ReceiverId, receiverIds));

        return await Collection
            .Find(filter)
            .ToListAsync(ct);
    }

    public async Task<Maybe<Invitation>> GetByGroupIdAndReceiverId(string receiverId, string groupId, CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.ReceiverId, receiverId),
            FilterBuilder.Eq(x => x.GroupId, groupId));

        return await Collection.Find(filter).SingleOrDefaultAsync(ct);
    }

    public async Task<Maybe<Invitation>> GetByGuestId(string guestId, string groupId, CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.GuestId, guestId),
            FilterBuilder.Eq(x => x.GroupId, groupId));

        return await Collection.Find(filter).SingleOrDefaultAsync(ct);
    }

    public async Task<Result> DeleteByGroupId(string groupId, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.GroupId, groupId);
        var result = await Collection.DeleteManyAsync(filter, ct);

        return result.IsAcknowledged ? Result.Success() : Result.Failure("Failed to delete group invitations");
    }

    public async Task<Result> DeleteByGroupIdAndReceiverId(string receiverId, string groupId, CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.ReceiverId, receiverId),
            FilterBuilder.Eq(x => x.GroupId, groupId));

        var result = await Collection.DeleteManyAsync(filter, ct);

        return result.IsAcknowledged ? Result.Success() : Result.Failure("Failed to delete invitations");
    }

    public async Task<Result> DeleteByGuestId(string guestId, string groupId, CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.GuestId, guestId),
            FilterBuilder.Eq(x => x.GroupId, groupId));

        var result = await Collection.DeleteManyAsync(filter, ct);

        return result.IsAcknowledged ? Result.Success() : Result.Failure("Failed to delete invitations");
    }

    public async Task<long> CountByReceiverIdAndMinCreated(string receiverId, DateTime minCreatedDate, CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.ReceiverId, receiverId),
            FilterBuilder.Gt(x => x.Created, minCreatedDate));

        return await Collection.Find(filter).CountDocumentsAsync(ct);
    }
}