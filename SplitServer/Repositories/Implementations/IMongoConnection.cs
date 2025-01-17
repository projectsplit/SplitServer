using MongoDB.Driver;

namespace SplitServer.Repositories.Implementations;

public interface IMongoConnection
{
    IMongoDatabase GetDatabase();
}