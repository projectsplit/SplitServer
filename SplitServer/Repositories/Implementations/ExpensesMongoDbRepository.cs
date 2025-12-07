using CSharpFunctionalExtensions;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using SplitServer.Models;
using SplitServer.Repositories.Implementations.Models;
using SplitServer.Repositories.Mappers;

namespace SplitServer.Repositories.Implementations;

public class ExpensesMongoDbRepository : MongoDbRepositoryBase<Expense, ExpenseMongoDbDocument>, IExpensesRepository
{
    public ExpensesMongoDbRepository(IMongoConnection mongoConnection) :
        base(
            mongoConnection,
            "Expenses",
            new ExpenseMapper())
    {
    }

    public async Task<List<GroupExpense>> GetByGroupId(
        string groupId,
        int pageSize,
        DateTime? maxOccurred,
        DateTime? maxCreated,
        CancellationToken ct)
    {
        var groupExpensesCollection = Collection.Database
            .GetCollection<GroupExpenseMongoDbDocument>(Collection.CollectionNamespace.CollectionName);

        var filterBuilder = Builders<GroupExpenseMongoDbDocument>.Filter;
        var sortBuilder = Builders<GroupExpenseMongoDbDocument>.Sort;

        var paginationFilter = maxOccurred is not null && maxCreated is not null
            ? filterBuilder.Or(
                filterBuilder.Lt(x => x.Occurred, maxOccurred),
                filterBuilder.And(
                    filterBuilder.Eq(x => x.Occurred, maxOccurred),
                    filterBuilder.Lt(x => x.Created, maxCreated)))
            : filterBuilder.Empty;

        var filter = filterBuilder.And(
            filterBuilder.Eq(x => x.GroupId, groupId),
            paginationFilter);

        var sort = sortBuilder.Descending(x => x.Occurred).Descending(x => x.Created);

        var documents = await groupExpensesCollection
            .Find(filter)
            .Sort(sort)
            .Limit(pageSize)
            .ToListAsync(ct);

        return documents.Select(d => (GroupExpense)Mapper.ToEntity(d)).ToList();
    }

    public async Task<List<GroupExpense>> GetAllByGroupId(string groupId, CancellationToken ct)
    {
        var groupExpensesCollection = Collection.Database
            .GetCollection<GroupExpenseMongoDbDocument>(Collection.CollectionNamespace.CollectionName);

        var filterBuilder = Builders<GroupExpenseMongoDbDocument>.Filter;

        var filter = filterBuilder.Eq(x => x.GroupId, groupId);

        var documents = await groupExpensesCollection
            .Find(filter)
            .ToListAsync(ct);

        return documents.Select(d => (GroupExpense)Mapper.ToEntity(d)).ToList();
    }

    public async Task<Dictionary<string, int>> GetLabelCounts(string groupId, CancellationToken ct)
    {
        var groupExpensesCollection = Collection.Database
            .GetCollection<GroupExpenseMongoDbDocument>(Collection.CollectionNamespace.CollectionName);

        var filterBuilder = Builders<GroupExpenseMongoDbDocument>.Filter;

        var filter = filterBuilder.Eq(x => x.GroupId, groupId);

        var result = await groupExpensesCollection
            .Aggregate()
            .Match(filter)
            .Unwind(x => x.Labels)
            .Group(
                x => x[nameof(Expense.Labels)],
                g => new
                {
                    LabelId = g.Key.ToString()!,
                    Count = g.Count()
                })
            .SortByDescending(x => x.Count)
            .ThenBy(x => x.LabelId)
            .ToListAsync(ct);

        return result.ToDictionary(x => x.LabelId, x => x.Count);
    }

    public async Task<Result> DeleteByGroupId(string groupId, CancellationToken ct)
    {
        var groupExpensesCollection = Collection.Database
            .GetCollection<GroupExpenseMongoDbDocument>(Collection.CollectionNamespace.CollectionName);

        var filterBuilder = Builders<GroupExpenseMongoDbDocument>.Filter;

        var filter = filterBuilder.Eq(x => x.GroupId, groupId);

        var result = await groupExpensesCollection.DeleteManyAsync(filter, null, ct);

        return result.IsAcknowledged ? Result.Success() : Result.Failure("Failed to delete group expenses");
    }

    public async Task<List<GroupExpense>> GetAllByMemberIds(List<string> memberIds, CancellationToken ct)
    {
        var groupExpensesCollection = Collection.Database
            .GetCollection<GroupExpenseMongoDbDocument>(Collection.CollectionNamespace.CollectionName);

        var filterBuilder = Builders<GroupExpenseMongoDbDocument>.Filter;

        var sharesFilter = filterBuilder.In("Shares.MemberId", memberIds);
        var paymentsFilter = filterBuilder.In("Payments.MemberId", memberIds);

        var filter = filterBuilder.Or(sharesFilter, paymentsFilter);

        var documents = await groupExpensesCollection
            .Find(filter)
            .ToListAsync(ct);

        return documents.Select(d => (GroupExpense)Mapper.ToEntity(d)).ToList();
    }

    public async Task<List<GroupExpense>> GetAllByMemberIds(
        List<string> memberIds,
        DateTime startDate,
        DateTime endDate,
        CancellationToken ct)
    {
        var sharesFilter = FilterBuilder.In("Shares.MemberId", memberIds);
        var paymentsFilter = FilterBuilder.In("Payments.MemberId", memberIds);
        var occurredFilter = FilterBuilder.And(
            FilterBuilder.Gte(x => x.Occurred, startDate),
            FilterBuilder.Lte(x => x.Occurred, endDate));

        var filter = FilterBuilder.And(
            FilterBuilder.Or(sharesFilter, paymentsFilter),
            occurredFilter);

        var documents = await Collection
            .Find(filter)
            .SortBy(x => x.Occurred)
            .ToListAsync(ct);

        return documents.Select(d => (GroupExpense)Mapper.ToEntity(d)).ToList();
    }

    public async Task<bool> ExistsInAnyExpense(string groupId, string memberId, CancellationToken ct)
    {
        var groupExpensesCollection = Collection.Database
            .GetCollection<GroupExpenseMongoDbDocument>(Collection.CollectionNamespace.CollectionName);

        var filterBuilder = Builders<GroupExpenseMongoDbDocument>.Filter;

        var filter = filterBuilder.And(
            filterBuilder.Eq(x => x.GroupId, groupId),
            filterBuilder.Or(
                filterBuilder.Eq("Shares.MemberId", memberId),
                filterBuilder.Eq("Payments.MemberId", memberId)));

        return await groupExpensesCollection.Find(filter).AnyAsync(ct);
    }

    public async Task<bool> LabelIsInUse(string groupId, string labelId, CancellationToken ct)
    {
        var groupExpensesCollection = Collection.Database
            .GetCollection<GroupExpenseMongoDbDocument>(Collection.CollectionNamespace.CollectionName);

        var filterBuilder = Builders<GroupExpenseMongoDbDocument>.Filter;

        var filter = filterBuilder.And(
            filterBuilder.Eq(x => x.GroupId, groupId),
            filterBuilder.AnyEq(x => x.Labels, labelId));

        return await groupExpensesCollection.Find(filter).AnyAsync(ct);
    }

    public async Task<List<GroupExpense>> Search(
        string groupId,
        string? searchTerm,
        DateTime? minTime,
        DateTime? maxTime,
        string[]? participantIds,
        string[]? payerIds,
        string[]? labelIds,
        int pageSize,
        DateTime? maxOccurred,
        DateTime? maxCreated,
        CancellationToken ct)
    {
        var groupExpensesCollection = Collection.Database
            .GetCollection<GroupExpenseMongoDbDocument>(Collection.CollectionNamespace.CollectionName);

        var filterBuilder = Builders<GroupExpenseMongoDbDocument>.Filter;
        var sortBuilder = Builders<GroupExpenseMongoDbDocument>.Sort;

        var paginationFilter = maxOccurred is not null && maxCreated is not null
            ? filterBuilder.Or(
                filterBuilder.Lt(x => x.Occurred, maxOccurred),
                filterBuilder.And(
                    filterBuilder.Eq(x => x.Occurred, maxOccurred),
                    filterBuilder.Lt(x => x.Created, maxCreated)))
            : filterBuilder.Empty;

        var participantsFilter = !participantIds.IsNullOrEmpty()
            ? filterBuilder.In("Shares.MemberId", participantIds)
            : filterBuilder.Empty;

        var payersFilter = !payerIds.IsNullOrEmpty()
            ? filterBuilder.In("Payments.MemberId", payerIds)
            : filterBuilder.Empty;

        var labelsFilter = !labelIds.IsNullOrEmpty()
            ? filterBuilder.AnyIn(x => x.Labels, labelIds)
            : filterBuilder.Empty;

        var minTimeFilter = minTime is not null
            ? filterBuilder.Gte(x => x.Occurred, minTime)
            : filterBuilder.Empty;

        var maxTimeFilter = maxTime is not null
            ? filterBuilder.Lte(x => x.Occurred, maxTime)
            : filterBuilder.Empty;

        var descriptionFilter = searchTerm is not null
            ? filterBuilder.Regex(x => x.Description, new BsonRegularExpression(searchTerm, "i"))
            : filterBuilder.Empty;

        var filter = filterBuilder.And(
            filterBuilder.Eq(x => x.GroupId, groupId),
            participantsFilter,
            payersFilter,
            descriptionFilter,
            labelsFilter,
            minTimeFilter,
            maxTimeFilter,
            paginationFilter);

        var sort = sortBuilder.Descending(x => x.Occurred).Descending(x => x.Created);

        var documents = await groupExpensesCollection
            .Find(filter)
            .Sort(sort)
            .Limit(pageSize)
            .ToListAsync(ct);

        return documents.Select(d => (GroupExpense)Mapper.ToEntity(d)).ToList();
    }
}