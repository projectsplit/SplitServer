using CSharpFunctionalExtensions;
using MongoDB.Driver;
using SplitServer.Models;
using SplitServer.Repositories.Implementations.Models;
using SplitServer.Repositories.Mappers;

namespace SplitServer.Repositories.Implementations;

public class RecurringExpensesMongoDbRepository :
    MongoDbRepositoryBase<RecurringExpense, RecurringExpenseMongoDbDocument>,
    IRecurringExpensesRepository
{
    private readonly IMongoCollection<GroupRecurringExpenseMongoDbDocument> _groupRecurringExpensesCollection;

    public RecurringExpensesMongoDbRepository(IMongoConnection mongoConnection) :
        base(
            mongoConnection,
            "RecurringExpenses",
            new RecurringExpenseMapper())
    {
        _groupRecurringExpensesCollection = Collection.Database
            .GetCollection<GroupRecurringExpenseMongoDbDocument>(Collection.CollectionNamespace.CollectionName);
    }

    public async Task<List<RecurringExpense>> GetAllByUserId(string userId, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.UserId, userId);

        var documents = await Collection
            .Find(filter)
            .SortByDescending(x => x.Created)
            .ToListAsync(ct);

        return documents.Select(Mapper.ToEntity).ToList();
    }

    public async Task<List<RecurringExpense>> GetDue(DateTime nowUtc, int limit, CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.IsPaused, false),
            FilterBuilder.Lte(x => x.NextOccurrence, nowUtc));

        var documents = await Collection
            .Find(filter)
            .SortBy(x => x.NextOccurrence)
            .Limit(limit)
            .ToListAsync(ct);

        return documents.Select(Mapper.ToEntity).ToList();
    }

    public async Task<Result<bool>> UpdateIfUnchanged(RecurringExpense entity, DateTime expectedUpdated, CancellationToken ct)
    {
        // The stamp compared here always comes from a document read back out of Mongo, so both
        // sides carry BSON's millisecond precision and equality is exact.
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.Id, entity.Id),
            FilterBuilder.Eq(x => x.Updated, expectedUpdated));

        var result = await Collection.ReplaceOneAsync(
            filter,
            Mapper.ToDocument(entity),
            new ReplaceOptions { IsUpsert = false },
            ct);

        return result.IsAcknowledged
            ? Result.Success(result.MatchedCount > 0)
            : Result.Failure<bool>("Update failed");
    }

    public async Task EnsureIndexes(CancellationToken ct)
    {
        // Backs the worker's once-a-minute due query, which filters on both fields. Paused
        // templates fall out of the index scan entirely instead of being skipped row by row.
        var dueIndex = new CreateIndexModel<RecurringExpenseMongoDbDocument>(
            Builders<RecurringExpenseMongoDbDocument>.IndexKeys
                .Ascending(x => x.IsPaused)
                .Ascending(x => x.NextOccurrence),
            new CreateIndexOptions { Name = "IsPaused_NextOccurrence" });

        // Backs the manage list, which is always one user's templates newest first.
        var byUserIndex = new CreateIndexModel<RecurringExpenseMongoDbDocument>(
            Builders<RecurringExpenseMongoDbDocument>.IndexKeys
                .Ascending(x => x.UserId)
                .Descending(x => x.Created),
            new CreateIndexOptions { Name = "UserId_Created" });

        await Collection.Indexes.CreateManyAsync([dueIndex, byUserIndex], ct);
    }

    public async Task<Result> DeleteByGroupId(string groupId, CancellationToken ct)
    {
        var filterBuilder = Builders<GroupRecurringExpenseMongoDbDocument>.Filter;

        var filter = filterBuilder.Eq(x => x.GroupId, groupId);

        var result = await _groupRecurringExpensesCollection.DeleteManyAsync(filter, null, ct);

        return result.IsAcknowledged ? Result.Success() : Result.Failure("Failed to delete group recurring expenses");
    }
}
