using CSharpFunctionalExtensions;
using MongoDB.Driver;
using SplitServer.Models;
using SplitServer.Repositories.Mappers;

namespace SplitServer.Repositories.Implementations;

public class UserLabelsMongoDbRepository : MongoDbRepositoryBase<UserLabel, UserLabel>, IUserLabelsRepository
{
    public UserLabelsMongoDbRepository(IMongoConnection mongoConnection) :
        base(
            mongoConnection,
            "UserLabels",
            new PassThroughMapper<UserLabel>())
    {
    }

    public async Task<List<UserLabel>> GetByUserId(string userId, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.UserId, userId);

        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<Result> DeleteByUserId(string userId, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.UserId, userId);

        var result = await Collection.DeleteManyAsync(filter, ct);

        return result.IsAcknowledged ? Result.Success() : Result.Failure("Failed to delete labels");
    }
}