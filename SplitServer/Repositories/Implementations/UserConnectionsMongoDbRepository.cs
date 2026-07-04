using CSharpFunctionalExtensions;
using MongoDB.Driver;
using SplitServer.Models;
using SplitServer.Repositories.Mappers;

namespace SplitServer.Repositories.Implementations;

public class UserConnectionsMongoDbRepository : MongoDbRepositoryBase<UserConnection, UserConnection>, IUserConnectionsRepository
{
    public UserConnectionsMongoDbRepository(IMongoConnection mongoConnection) :
        base(
            mongoConnection,
            "UserConnections",
            new PassThroughMapper<UserConnection>())
    {
    }

    public async Task<Maybe<UserConnection>> GetBetweenUsers(string userIdA, string userIdB, CancellationToken ct)
    {
        var filter = FilterBuilder.Or(
            FilterBuilder.And(
                FilterBuilder.Eq(x => x.SenderId, userIdA),
                FilterBuilder.Eq(x => x.ReceiverId, userIdB)),
            FilterBuilder.And(
                FilterBuilder.Eq(x => x.SenderId, userIdB),
                FilterBuilder.Eq(x => x.ReceiverId, userIdA)));

        var document = await Collection
            .Find(filter)
            .FirstOrDefaultAsync(ct);

        return document is not null
            ? document
            : Maybe<UserConnection>.None;
    }

    public async Task<List<UserConnection>> GetAllBetweenUsers(string userId, IList<string> otherUserIds, CancellationToken ct)
    {
        var filter = FilterBuilder.Or(
            FilterBuilder.And(
                FilterBuilder.Eq(x => x.SenderId, userId),
                FilterBuilder.In(x => x.ReceiverId, otherUserIds)),
            FilterBuilder.And(
                FilterBuilder.In(x => x.SenderId, otherUserIds),
                FilterBuilder.Eq(x => x.ReceiverId, userId)));

        return await Collection
            .Find(filter)
            .ToListAsync(ct);
    }

    public async Task<List<string>> GetAcceptedUserIds(string userId, CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.Status, ConnectionStatus.Accepted),
            FilterBuilder.Or(
                FilterBuilder.Eq(x => x.SenderId, userId),
                FilterBuilder.Eq(x => x.ReceiverId, userId)));

        var connections = await Collection
            .Find(filter)
            .ToListAsync(ct);

        return connections
            .Select(x => x.SenderId == userId ? x.ReceiverId : x.SenderId)
            .Distinct()
            .ToList();
    }

    public async Task<List<UserConnection>> GetPendingByReceiverId(
        string receiverId,
        int pageSize,
        DateTime maxCreatedDate,
        CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.ReceiverId, receiverId),
            FilterBuilder.Eq(x => x.Status, ConnectionStatus.Pending),
            FilterBuilder.Lt(x => x.Created, maxCreatedDate));

        return await Collection
            .Find(filter)
            .SortByDescending(x => x.Created)
            .Limit(pageSize)
            .ToListAsync(ct);
    }

    public async Task<long> CountPendingByReceiverIdAndMinCreated(
        string receiverId,
        DateTime minCreatedDate,
        CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.ReceiverId, receiverId),
            FilterBuilder.Eq(x => x.Status, ConnectionStatus.Pending),
            FilterBuilder.Gt(x => x.Created, minCreatedDate));

        return await Collection.CountDocumentsAsync(filter, null, ct);
    }
}
