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

    public async Task<Maybe<Invitation>> Get(string fromId, string toId, string groupId, CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.FromId, fromId),
            FilterBuilder.Eq(x => x.ToId, toId),
            FilterBuilder.Eq(x => x.GroupId, groupId));

        return await Collection.Find(filter).SingleOrDefaultAsync(ct);
    }

    public async Task<Maybe<Invitation>> GetByToId(string toId, string groupId, CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.ToId, toId),
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
}