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

    /// <summary>
    /// The fields of a user that are never saved along with the rest of it. Each has one writer of
    /// its own, a field-level method on this class, and can change at any moment without the account
    /// holder doing anything: a renewal or a gift reported by Google, a grant made on boot, a notice
    /// going out from the worker.
    /// </summary>
    private static readonly string[] FieldsWrittenOnTheirOwn =
    [
        nameof(User.Plus),
        nameof(User.Donations),
        nameof(User.PlusForLife),
        nameof(User.PlusTrialReminderStage),
    ];

    /// <summary>
    /// Saves the account's own fields and leaves the ones in <see cref="FieldsWrittenOnTheirOwn"/>
    /// exactly as they are in the database.
    /// </summary>
    /// <remarks>
    /// Everywhere else an update replaces the whole document with the copy the caller read a moment
    /// earlier. Here that copy can be out of date in a way the caller cannot know about: a renewal
    /// or a gift reported by Google lands on those fields at any time, from a notification or the
    /// worker, and a replace would put back whatever the caller happened to read — or, read before
    /// the first purchase, take the Plus mark off someone who has just paid for it. Leaving them out
    /// of the write makes that impossible rather than unlikely.
    ///
    /// Built from the serialised document rather than a list of fields, so a property added to
    /// <see cref="User"/> later is saved without anyone having to remember this method exists.
    /// </remarks>
    public override async Task<Result> Update(User entity, CancellationToken ct)
    {
        var fields = entity.ToBsonDocument();

        fields.Remove("_id");

        foreach (var field in FieldsWrittenOnTheirOwn)
        {
            fields.Remove(field);
        }

        var result = await Collection.UpdateOneAsync(
            FilterBuilder.Eq(x => x.Id, entity.Id),
            new BsonDocument("$set", fields),
            new UpdateOptions { IsUpsert = false },
            ct);

        return result.MatchedCount > 0 ? Result.Success() : Result.Failure("Update failed");
    }

    public async Task<Result> SetPlus(string userId, UserPlus? plus, CancellationToken ct)
    {
        var update = plus is not null
            ? UpdateBuilder.Set(x => x.Plus, plus)
            : UpdateBuilder.Unset(x => x.Plus);

        return await UpdateField(userId, update, "Plus", ct);
    }

    public async Task<Result> SetDonations(string userId, UserDonations? donations, CancellationToken ct)
    {
        var update = donations is not null
            ? UpdateBuilder.Set(x => x.Donations, donations)
            : UpdateBuilder.Unset(x => x.Donations);

        return await UpdateField(userId, update, "Donations", ct);
    }

    public async Task<Result> SetPlusForLife(string userId, DateTime? grantedAt, CancellationToken ct)
    {
        var update = grantedAt is not null
            ? UpdateBuilder.Set(x => x.PlusForLife, grantedAt)
            : UpdateBuilder.Unset(x => x.PlusForLife);

        return await UpdateField(userId, update, "PlusForLife", ct);
    }

    public async Task<Result<long>> GrantPlusForLifeToAccountsCreatedBefore(
        DateTime cutOff,
        DateTime grantedAt,
        CancellationToken ct)
    {
        // Eq null matches an account where the field is missing as well as one where it is null,
        // which is every account that has never been granted anything. One that already has a
        // value keeps it: the date it was first given is the one worth knowing.
        var filter = FilterBuilder.And(
            FilterBuilder.Lt(x => x.Created, cutOff),
            FilterBuilder.Eq(x => x.PlusForLife, null));

        var result = await Collection.UpdateManyAsync(
            filter,
            UpdateBuilder.Set(x => x.PlusForLife, grantedAt),
            cancellationToken: ct);

        return result.IsAcknowledged
            ? Result.Success(result.ModifiedCount)
            : Result.Failure<long>("Granting Plus for life failed");
    }

    public async Task<List<User>> GetTrialReminderCandidates(
        DateTime createdOnOrBefore,
        int belowStage,
        DateTime? createdOnOrAfter,
        int limit,
        CancellationToken ct)
    {
        var filters = new List<FilterDefinition<User>>
        {
            FilterBuilder.Lte(x => x.Created, createdOnOrBefore),
            NotYetAtStage(belowStage),
            FilterBuilder.Eq(x => x.PlusForLife, null),
        };

        if (createdOnOrAfter is { } from)
        {
            filters.Add(FilterBuilder.Gte(x => x.Created, from));
        }

        return await Collection
            .Find(FilterBuilder.And(filters))
            .SortBy(x => x.Created)
            .Limit(limit)
            .ToListAsync(ct);
    }

    public async Task<bool> TryAdvanceTrialReminderStage(string userId, int toStage, CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.Id, userId),
            NotYetAtStage(toStage));

        var result = await Collection.UpdateOneAsync(
            filter,
            UpdateBuilder.Set(x => x.PlusTrialReminderStage, toStage),
            new UpdateOptions { IsUpsert = false },
            ct);

        return result.IsAcknowledged && result.ModifiedCount == 1;
    }

    public async Task EnsureIndexes(CancellationToken ct)
    {
        // Backs the worker's hourly search for accounts nearing the end of their free days, which is
        // a range over creation time. Without it that search reads every account there is.
        var byCreatedIndex = new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(x => x.Created));

        await Collection.Indexes.CreateOneAsync(byCreatedIndex, cancellationToken: ct);
    }

    /// <summary>An account with no stage recorded at all has had no notice, the same as one at 0.</summary>
    private static FilterDefinition<User> NotYetAtStage(int stage) =>
        FilterBuilder.Or(
            FilterBuilder.Eq(x => x.PlusTrialReminderStage, null),
            FilterBuilder.Lt(x => x.PlusTrialReminderStage, stage));

    /// <summary>
    /// One field of one user, and never an upsert. Matching nothing is a success: the only way to
    /// get here for a user who is not there is an account deleted while its subscription or its
    /// gifts were still being talked about by Google, and there is nothing to write that onto.
    /// </summary>
    private async Task<Result> UpdateField(
        string userId,
        UpdateDefinition<User> update,
        string what,
        CancellationToken ct)
    {
        var result = await Collection.UpdateOneAsync(
            FilterBuilder.Eq(x => x.Id, userId),
            update,
            new UpdateOptions { IsUpsert = false },
            ct);

        return result.IsAcknowledged ? Result.Success() : Result.Failure($"Setting {what} on the user failed");
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