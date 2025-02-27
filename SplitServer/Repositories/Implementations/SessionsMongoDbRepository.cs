using CSharpFunctionalExtensions;
using MongoDB.Driver;
using SplitServer.Models;
using SplitServer.Repositories.Mappers;

namespace SplitServer.Repositories.Implementations;

public class SessionsMongoDbRepository : MongoDbRepositoryBase<Session, Session>, ISessionsRepository
{
    public SessionsMongoDbRepository(IMongoConnection mongoConnection) :
        base(
            mongoConnection,
            "Sessions",
            new PassThroughMapper<Session>())
    {
    }

    public async Task<Maybe<Session>> GetByRefreshToken(string refreshToken, CancellationToken ct)
    {
        var filter = Builders<Session>.Filter.Eq(x => x.RefreshToken, refreshToken);

        return await Collection.Find(filter).SingleOrDefaultAsync(ct);
    }

    public async Task<Result> DeleteByRefreshToken(string refreshToken, CancellationToken ct)
    {
        var filter = Builders<Session>.Filter.Eq(x => x.RefreshToken, refreshToken);

        var deleteResult = await Collection.DeleteManyAsync(filter, ct);

        if (!deleteResult.IsAcknowledged)
        {
            return Result.Failure<Result>("Failed to delete session");
        }

        return Result.Success();
    }
}