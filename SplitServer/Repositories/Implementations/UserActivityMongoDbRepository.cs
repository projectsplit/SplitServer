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
}