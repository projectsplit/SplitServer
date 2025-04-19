using CSharpFunctionalExtensions;
using MongoDB.Driver;
using MongoDB.Driver.Search;
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

    public async Task<List<User>> SearchByUsername(string keyword, int skip, int pageSize, CancellationToken ct)
    {
        var search = SearchBuilder.Autocomplete(
            x => x.Username,
            new SingleSearchQueryDefinition(keyword),
            fuzzy: new SearchFuzzyOptions { MaxEdits = 1, PrefixLength = 4 });

        var pipelineDefinition = PipelineBuilder
            .Search(search)
            .Skip(skip)
            .Limit(pageSize);

        return await Collection
            .Aggregate(pipelineDefinition, cancellationToken: ct)
            .ToListAsync(ct);
    }

    public async Task<List<User>> GetLatestUsers(int skip, int pageSize, CancellationToken ct)
    {
        return await Collection
            .Find(FilterBuilder.Empty)
            .SortByDescending(x => x.Created)
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync(ct);
    }

    public async Task<bool> AnyWithUsername(string username, CancellationToken ct)
    {
        return await Collection
            .Find(FilterBuilder.Eq(x => x.Username, username))
            .AnyAsync(ct);
    }
}