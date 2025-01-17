using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SplitServer.Configuration;

namespace SplitServer.Repositories.Implementations;

public class MongoConnection : IMongoConnection
{
    private readonly MongoClient _mongoClient;
    private readonly string _databaseName;

    public MongoConnection(IOptions<MongoDbSettings> settings)
    {
        _databaseName = settings.Value.DatabaseName;
        _mongoClient = new MongoClient(settings.Value.ConnectionString);
    }

    public IMongoDatabase GetDatabase()
    {
        return _mongoClient.GetDatabase(_databaseName);
    }
}