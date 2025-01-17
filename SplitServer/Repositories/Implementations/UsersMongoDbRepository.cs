using CSharpFunctionalExtensions;
using MongoDB.Driver;
using SplitServer.Models;
using SplitServer.Repositories.Mappers;

namespace SplitServer.Repositories.Implementations;

public class UsersMongoDbRepository : MongoDbRepositoryBase<User, User>, IUsersRepository
{
    public UsersMongoDbRepository(IMongoConnection mongoConnection) :
        base(
            mongoConnection,
            "Users",
            new PassThroughMapper<User>())
    {
    }

    public async Task<Maybe<User>> GetByEmail(string email, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.Email, email);

        return await Collection.Find(filter).SingleOrDefaultAsync(ct);
    }

    public async Task<Maybe<User>> GetByUsername(string username, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.Username, username);

        return await Collection.Find(filter).SingleOrDefaultAsync(ct);
    }

    public async Task<Maybe<User>> GetByGoogleId(string googleId, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.GoogleId, googleId);

        return await Collection.Find(filter).SingleOrDefaultAsync(ct);
    }
}