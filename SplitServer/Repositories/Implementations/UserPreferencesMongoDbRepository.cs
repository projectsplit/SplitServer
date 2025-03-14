using SplitServer.Models;
using SplitServer.Repositories.Mappers;

namespace SplitServer.Repositories.Implementations;

public class UserPreferencesMongoDbRepository : MongoDbRepositoryBase<UserPreferences, UserPreferences>, IUserPreferencesRepository
{
    public UserPreferencesMongoDbRepository(IMongoConnection mongoConnection) :
        base(
            mongoConnection,
            "UserPreferences",
            new PassThroughMapper<UserPreferences>())
    {
    }
}