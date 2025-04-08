using CSharpFunctionalExtensions;
using MongoDB.Driver;
using SplitServer.Models;
using SplitServer.Repositories.Mappers;

namespace SplitServer.Repositories.Implementations;

public class UserActivityMongoDbRepository : MongoDbRepositoryBase<UserActivity, UserActivity>, IUserActivityRepository
{
    public UserActivityMongoDbRepository(IMongoConnection mongoConnection) :
        base(
            mongoConnection,
            "UserActivity",
            new PassThroughMapper<UserActivity>())
    {
    }

    public async Task<Result> ClearRecentGroupForUser(string userId, string groupId, CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.Id, userId),
            FilterBuilder.Eq(x => x.RecentGroupId, groupId));

        var update = UpdateBuilder.Set(x => x.RecentGroupId, null);

        var result = await Collection.UpdateManyAsync(filter, update, null, ct);

        return result.IsAcknowledged
            ? Result.Success()
            : Result.Failure("Failed to clear recent group for user");
    }

    public async Task<Result> ClearRecentGroupForAllUsers(string groupId, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.RecentGroupId, groupId);

        var update = UpdateBuilder.Set(x => x.RecentGroupId, null);

        var result = await Collection.UpdateManyAsync(filter, update, null, ct);

        return result.IsAcknowledged
            ? Result.Success()
            : Result.Failure("Failed to clear recent group for all users");
    }
}