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

    public async Task<Result> DeleteByGroupId(string groupId, CancellationToken ct)
    {
        var filterBuilder = Builders<GroupRecurringExpenseMongoDbDocument>.Filter;

        var filter = filterBuilder.Eq(x => x.GroupId, groupId);

        var result = await _groupRecurringExpensesCollection.DeleteManyAsync(filter, null, ct);

        return result.IsAcknowledged ? Result.Success() : Result.Failure("Failed to delete group recurring expenses");
    }
}
