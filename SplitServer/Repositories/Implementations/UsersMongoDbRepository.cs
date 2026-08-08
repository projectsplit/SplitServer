using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;
using MongoDB.Bson;
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

    public async Task<Maybe<User>> GetVerifiedByEmail(string email, CancellationToken ct)
    {
        // Verification enforces a single owner per email, but this sorts rather than using
        // SingleOrDefault so that pre-existing duplicates resolve to the earliest claim
        // instead of throwing on every password reset and username recovery.
        return await Collection
            .Find(VerifiedEmailFilter(email))
            .SortBy(x => x.Created)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Maybe<User>> GetByUsername(string username, CancellationToken ct)
    {
        return await Collection.Find(UsernameFilter(username)).SingleOrDefaultAsync(ct);
    }

    public async Task<Maybe<User>> GetByGoogleId(string googleId, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.GoogleId, googleId);

        return await Collection.Find(filter).SingleOrDefaultAsync(ct);
    }

    public async Task<List<User>> SearchByUsername(string keyword, UserIdScope scope, int skip, int pageSize, CancellationToken ct)
    {
        var search = SearchBuilder.Autocomplete(
            x => x.Username,
            new SingleSearchQueryDefinition(keyword),
            fuzzy: new SearchFuzzyOptions { MaxEdits = 1, PrefixLength = 4 });

        // The scope has to be matched after $search rather than folded into it: $search only runs
        // as the first stage of a pipeline, so narrowing by id is a stage of its own.
        var pipelineDefinition = PipelineBuilder
            .Search(search)
            .Match(ScopeFilter(scope))
            .Skip(skip)
            .Limit(pageSize);

        return await Collection
            .Aggregate(pipelineDefinition, cancellationToken: ct)
            .ToListAsync(ct);
    }

    public async Task<List<User>> GetLatestUsers(UserIdScope scope, int skip, int pageSize, CancellationToken ct)
    {
        return await Collection
            .Find(ScopeFilter(scope))
            .SortByDescending(x => x.Created)
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync(ct);
    }

    public async Task<bool> AnyWithUsername(string username, CancellationToken ct)
    {
        return await Collection
            .Find(UsernameFilter(username))
            .AnyAsync(ct);
    }

    private static FilterDefinition<User> ScopeFilter(UserIdScope scope)
    {
        return scope.Kind switch
        {
            UserIdScopeKind.Only => FilterBuilder.In(x => x.Id, scope.Ids),
            UserIdScopeKind.Except => FilterBuilder.Nin(x => x.Id, scope.Ids),
            _ => FilterBuilder.Empty
        };
    }

    private static FilterDefinition<User> UsernameFilter(string username)
    {
        return FilterBuilder.Regex(x => x.Username, new BsonRegularExpression($"^{Regex.Escape(username)}$", "i"));
    }

    private static FilterDefinition<User> VerifiedEmailFilter(string email)
    {
        return FilterBuilder.And(
            FilterBuilder.Regex(x => x.Email, new BsonRegularExpression($"^{Regex.Escape(email)}$", "i")),
            FilterBuilder.Eq(x => x.EmailVerified, true));
    }
}