using CSharpFunctionalExtensions;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using SplitServer.Models;
using SplitServer.Queries.Models;
using SplitServer.Repositories.Implementations.Models;
using SplitServer.Repositories.Mappers;

namespace SplitServer.Repositories.Implementations;

public class ExpensesMongoDbRepository : MongoDbRepositoryBase<Expense, ExpenseMongoDbDocument>, IExpensesRepository
{
    private readonly IMongoCollection<GroupExpenseMongoDbDocument> _groupExpensesCollection;
    private readonly IMongoCollection<NonGroupExpenseMongoDbDocument> _nonGroupExpensesCollection;

    public ExpensesMongoDbRepository(IMongoConnection mongoConnection) :
        base(
            mongoConnection,
            "Expenses",
            new ExpenseMapper())
    {
        _groupExpensesCollection = Collection.Database
            .GetCollection<GroupExpenseMongoDbDocument>(Collection.CollectionNamespace.CollectionName);

        _nonGroupExpensesCollection = Collection.Database
            .GetCollection<NonGroupExpenseMongoDbDocument>(Collection.CollectionNamespace.CollectionName);
    }

    private static FilterDefinition<TDocument> BuildPaginationFilter<TDocument>(
        FilterDefinitionBuilder<TDocument> filterBuilder,
        DateTime? occurred,
        DateTime? created,
        PaginationDirection direction,
        bool inclusive)
    {
        if (occurred is null || created is null) return filterBuilder.Empty;

        var fieldOccurred = "Occurred";
        var fieldCreated = "Created";

        if (direction == PaginationDirection.Older)
        {
            return filterBuilder.Or(
                filterBuilder.Lt(fieldOccurred, occurred),
                filterBuilder.And(
                    filterBuilder.Eq(fieldOccurred, occurred),
                    inclusive ? filterBuilder.Lte(fieldCreated, created) : filterBuilder.Lt(fieldCreated, created)));
        }
        else
        {
            return filterBuilder.Or(
                filterBuilder.Gt(fieldOccurred, occurred),
                filterBuilder.And(
                    filterBuilder.Eq(fieldOccurred, occurred),
                    inclusive ? filterBuilder.Gte(fieldCreated, created) : filterBuilder.Gt(fieldCreated, created)));
        }
    }

    public async Task<List<GroupExpense>> GetByGroupId(
        string groupId,
        int pageSize,
        DateTime? occurred,
        DateTime? created,
        PaginationDirection direction,
        bool inclusive,
        CancellationToken ct)
    {
        var filterBuilder = Builders<GroupExpenseMongoDbDocument>.Filter;
        var sortBuilder = Builders<GroupExpenseMongoDbDocument>.Sort;

        var paginationFilter = BuildPaginationFilter(filterBuilder, occurred, created, direction, inclusive);

        var filter = filterBuilder.And(
            filterBuilder.Eq(x => x.GroupId, groupId),
            paginationFilter);

        var sort = direction == PaginationDirection.Older
            ? sortBuilder.Descending(x => x.Occurred).Descending(x => x.Created)
            : sortBuilder.Ascending(x => x.Occurred).Ascending(x => x.Created);

        var documents = await _groupExpensesCollection
            .Find(filter)
            .Sort(sort)
            .Limit(pageSize)
            .ToListAsync(ct);

        var results = documents.Select(d => (GroupExpense)Mapper.ToEntity(d)).ToList();

        if (direction == PaginationDirection.Newer)
        {
            results.Reverse();
        }

        return results;
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

    public async Task<List<NonGroupExpense>> GetAllByUserId(string userId, CancellationToken ct)
    {
        var nonGroupExpensesCollection =
            Collection.Database.GetCollection<NonGroupExpenseMongoDbDocument>(Collection.CollectionNamespace
                .CollectionName);

        var filterBuilder = Builders<NonGroupExpenseMongoDbDocument>.Filter;

        var filter = filterBuilder.Or(
            filterBuilder.ElemMatch(x => x.Payments, p => p.UserId == userId),
            filterBuilder.ElemMatch(x => x.Shares, s => s.UserId == userId)
        );

        var documents = await nonGroupExpensesCollection.Find(filter).ToListAsync(ct);
        return documents.Select(d => (NonGroupExpense)Mapper.ToEntity(d)).ToList();
    }

    public async Task<List<Expense>> GetAllPersonalByUserId( string userId,
        List<string> memberIds,
        CancellationToken ct)
    {
        var expensesCollection =
            Collection.Database.GetCollection<ExpenseMongoDbDocument>(Collection.CollectionNamespace
                .CollectionName);
        
        var filterBuilder = Builders<ExpenseMongoDbDocument>.Filter;

        var userRelatedFilter = filterBuilder.Or(
            filterBuilder.And(filterBuilder.Eq("_t", "personal"), filterBuilder.Eq(x => x.CreatorId, userId)),
            filterBuilder.And(filterBuilder.Eq("_t", "non_group"), filterBuilder.Eq("Shares.UserId", userId)),
            filterBuilder.And(filterBuilder.Eq("_t", "group"), filterBuilder.In("Shares.MemberId", memberIds))
        );
        
        var documents = await expensesCollection.Find(userRelatedFilter).ToListAsync(ct);
        return documents.Select(d => Mapper.ToEntity(d)).ToList();
    }

    public async Task<Dictionary<string, int>> GetLabelCounts(string groupId, CancellationToken ct)
    {
        var filterBuilder = Builders<GroupExpenseMongoDbDocument>.Filter;

        var filter = filterBuilder.Eq(x => x.GroupId, groupId);

        var result = await _groupExpensesCollection
            .Aggregate()
            .Match(filter)
            .Unwind(x => x.Labels)
            .Group(
                x => x[nameof(GroupExpense.Labels)],
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
        var filterBuilder = Builders<GroupExpenseMongoDbDocument>.Filter;

        var filter = filterBuilder.Eq(x => x.GroupId, groupId);

        var result = await _groupExpensesCollection.DeleteManyAsync(filter, null, ct);

        return result.IsAcknowledged ? Result.Success() : Result.Failure("Failed to delete group expenses");
    }

    public async Task<List<GroupExpense>> GetAllByMemberIds(List<string> memberIds, CancellationToken ct)
    {
        var filterBuilder = Builders<GroupExpenseMongoDbDocument>.Filter;

        var sharesFilter = filterBuilder.In("Shares.MemberId", memberIds);
        var paymentsFilter = filterBuilder.In("Payments.MemberId", memberIds);

        var filter = filterBuilder.Or(sharesFilter, paymentsFilter);

        var documents = await _groupExpensesCollection
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

    public async Task<List<NonGroupExpense>> GetAllNonGroupExpensesByUserId(string userId, DateTime startDate,
        DateTime endDate, CancellationToken ct)
    {
        var filterBuilder = Builders<NonGroupExpenseMongoDbDocument>.Filter;

        var involvedFilter = filterBuilder.Or(
            filterBuilder.ElemMatch(x => x.Payments, p => p.UserId == userId),
            filterBuilder.ElemMatch(x => x.Shares, s => s.UserId == userId)
        );

        var occurredFilter = filterBuilder.And(
            filterBuilder.Gte(x => x.Occurred, startDate),
            filterBuilder.Lte(x => x.Occurred, endDate));

        var filter = filterBuilder.And(involvedFilter, occurredFilter);

        var documents = await _nonGroupExpensesCollection
            .Find(filter)
            .SortBy(x => x.Occurred)
            .ToListAsync(ct);

        return documents.Select(d => (NonGroupExpense)Mapper.ToEntity(d)).ToList();
    }

    public async Task<bool> ExistsInAnyExpense(string groupId, string memberId, CancellationToken ct)
    {
        var filterBuilder = Builders<GroupExpenseMongoDbDocument>.Filter;

        var filter = filterBuilder.And(
            filterBuilder.Eq(x => x.GroupId, groupId),
            filterBuilder.Or(
                filterBuilder.Eq("Shares.MemberId", memberId),
                filterBuilder.Eq("Payments.MemberId", memberId)));

        return await _groupExpensesCollection.Find(filter).AnyAsync(ct);
    }

    public async Task<bool> LabelIsInUse(string groupId, string labelId, CancellationToken ct)
    {
        var filterBuilder = Builders<GroupExpenseMongoDbDocument>.Filter;

        var filter = filterBuilder.And(
            filterBuilder.Eq(x => x.GroupId, groupId),
            filterBuilder.AnyEq(x => x.Labels, labelId));

        return await _groupExpensesCollection.Find(filter).AnyAsync(ct);
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
        DateTime? occurred,
        DateTime? created,
        PaginationDirection direction,
        bool inclusive,
        CancellationToken ct)
    {
        var filterBuilder = Builders<GroupExpenseMongoDbDocument>.Filter;
        var sortBuilder = Builders<GroupExpenseMongoDbDocument>.Sort;

        var paginationFilter = BuildPaginationFilter(filterBuilder, occurred, created, direction, inclusive);

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

        var sort = direction == PaginationDirection.Older
            ? sortBuilder.Descending(x => x.Occurred).Descending(x => x.Created)
            : sortBuilder.Ascending(x => x.Occurred).Ascending(x => x.Created);

        var documents = await _groupExpensesCollection
            .Find(filter)
            .Sort(sort)
            .Limit(pageSize)
            .ToListAsync(ct);

        var results = documents.Select(d => (GroupExpense)Mapper.ToEntity(d)).ToList();

        if (direction == PaginationDirection.Newer)
        {
            results.Reverse();
        }

        return results;
    }

    public async Task<List<NonGroupExpense>> GetNonGroupByUserId(
        string userId,
        int pageSize,
        DateTime? occurred,
        DateTime? created,
        PaginationDirection direction,
        bool inclusive,
        CancellationToken ct)
    {
        var filterBuilder = Builders<NonGroupExpenseMongoDbDocument>.Filter;
        var sortBuilder = Builders<NonGroupExpenseMongoDbDocument>.Sort;

        var paginationFilter = BuildPaginationFilter(filterBuilder, occurred, created, direction, inclusive);

        var userFilter = filterBuilder.Or(
            filterBuilder.ElemMatch(x => x.Shares, share => share.UserId == userId),
            filterBuilder.ElemMatch(x => x.Payments, payment => payment.UserId == userId));

        var filter = filterBuilder.And(
            userFilter,
            paginationFilter);

        var sort = direction == PaginationDirection.Older
            ? sortBuilder.Descending(x => x.Occurred).Descending(x => x.Created)
            : sortBuilder.Ascending(x => x.Occurred).Ascending(x => x.Created);

        var documents = await _nonGroupExpensesCollection
            .Find(filter)
            .Sort(sort)
            .Limit(pageSize)
            .ToListAsync(ct);

        var results = documents.Select(d => (NonGroupExpense)Mapper.ToEntity(d)).ToList();

        if (direction == PaginationDirection.Newer)
        {
            results.Reverse();
        }

        return results;
    }

    public async Task<List<Expense>> GetPersonalByUserId(
        string userId,
        List<string> memberIds,
        int pageSize,
        DateTime? occurred,
        DateTime? created,
        PaginationDirection direction,
        bool inclusive,
        CancellationToken ct)
    {
        var filterBuilder = Builders<ExpenseMongoDbDocument>.Filter;
        var sortBuilder = Builders<ExpenseMongoDbDocument>.Sort;

        var paginationFilter = BuildPaginationFilter(filterBuilder, occurred, created, direction, inclusive);

        var userRelatedFilter = filterBuilder.Or(
            filterBuilder.And(filterBuilder.Eq("_t", "personal"), filterBuilder.Eq(x => x.CreatorId, userId)),
            filterBuilder.And(filterBuilder.Eq("_t", "non_group"), filterBuilder.Eq("Shares.UserId", userId)),
            filterBuilder.And(filterBuilder.Eq("_t", "group"), filterBuilder.In("Shares.MemberId", memberIds))
        );

        var filter = filterBuilder.And(userRelatedFilter, paginationFilter);

        var sort = direction == PaginationDirection.Older
            ? sortBuilder.Descending(x => x.Occurred).Descending(x => x.Created)
            : sortBuilder.Ascending(x => x.Occurred).Ascending(x => x.Created);

        var documents = await Collection
            .Find(filter)
            .Sort(sort)
            .Limit(pageSize)
            .ToListAsync(ct);

        var results = documents.Select(d => Mapper.ToEntity(d)).ToList();

        if (direction == PaginationDirection.Newer)
        {
            results.Reverse();
        }

        return results;
    }

    public async Task<List<Expense>> SearchPersonalByUserId(
        string userId,
        List<string> memberIds,
        string? searchTerm,
        DateTime? minTime,
        DateTime? maxTime,
        string[]? labels,
        int pageSize,
        DateTime? occurred,
        DateTime? created,
        PaginationDirection direction,
        bool inclusive,
        CancellationToken ct)
    {
        var filterBuilder = Builders<ExpenseMongoDbDocument>.Filter;
        var sortBuilder = Builders<ExpenseMongoDbDocument>.Sort;

        var paginationFilter = BuildPaginationFilter(filterBuilder, occurred, created, direction, inclusive);

        var userRelatedFilter = filterBuilder.Or(
            filterBuilder.And(filterBuilder.Eq("_t", "personal"), filterBuilder.Eq(x => x.CreatorId, userId)),
            filterBuilder.And(filterBuilder.Eq("_t", "non_group"), filterBuilder.Eq("Shares.UserId", userId)),
            filterBuilder.And(filterBuilder.Eq("_t", "group"), filterBuilder.In("Shares.MemberId", memberIds))
        );

        var descriptionFilter = searchTerm is not null
            ? filterBuilder.Regex(x => x.Description, new BsonRegularExpression(searchTerm, "i"))
            : filterBuilder.Empty;

        var labelsFilter = !labels.IsNullOrEmpty()
            ? filterBuilder.AnyIn(x => x.Labels, labels)
            : filterBuilder.Empty;

        var minTimeFilter = minTime is not null
            ? filterBuilder.Gte(x => x.Occurred, minTime)
            : filterBuilder.Empty;

        var maxTimeFilter = maxTime is not null
            ? filterBuilder.Lte(x => x.Occurred, maxTime)
            : filterBuilder.Empty;

        var filter = filterBuilder.And(
            userRelatedFilter,
            descriptionFilter,
            labelsFilter,
            minTimeFilter,
            maxTimeFilter,
            paginationFilter);

        var sort = direction == PaginationDirection.Older
            ? sortBuilder.Descending(x => x.Occurred).Descending(x => x.Created)
            : sortBuilder.Ascending(x => x.Occurred).Ascending(x => x.Created);

        var documents = await Collection
            .Find(filter)
            .Sort(sort)
            .Limit(pageSize)
            .ToListAsync(ct);

        var results = documents.Select(d => Mapper.ToEntity(d)).ToList();

        if (direction == PaginationDirection.Newer)
        {
            results.Reverse();
        }

        return results;
    }

    public async Task<List<NonGroupExpense>> SearchNonGroup(
        string userId,
        string? searchTerm,
        DateTime? minTime,
        DateTime? maxTime,
        string[]? participantIds,
        string[]? payerIds,
        string[]? labels,
        int pageSize,
        DateTime? occurred,
        DateTime? created,
        PaginationDirection direction,
        bool inclusive,
        CancellationToken ct)
    {
        var filterBuilder = Builders<NonGroupExpenseMongoDbDocument>.Filter;
        var sortBuilder = Builders<NonGroupExpenseMongoDbDocument>.Sort;

        var paginationFilter = BuildPaginationFilter(filterBuilder, occurred, created, direction, inclusive);

        var participantsFilter = !participantIds.IsNullOrEmpty()
            ? filterBuilder.In("Shares.UserId", participantIds)
            : filterBuilder.Empty;

        var payersFilter = !payerIds.IsNullOrEmpty()
            ? filterBuilder.In("Payments.UserId", payerIds)
            : filterBuilder.Empty;

        var labelsFilter = !labels.IsNullOrEmpty()
            ? filterBuilder.AnyIn(x => x.Labels, labels)
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

        var userFilter = filterBuilder.Or(
            filterBuilder.ElemMatch(x => x.Shares, s => s.UserId == userId),
            filterBuilder.ElemMatch(x => x.Payments, p => p.UserId == userId));

        var filter = filterBuilder.And(
            userFilter,
            participantsFilter,
            payersFilter,
            descriptionFilter,
            labelsFilter,
            minTimeFilter,
            maxTimeFilter,
            paginationFilter);

        var sort = direction == PaginationDirection.Older
            ? sortBuilder.Descending(x => x.Occurred).Descending(x => x.Created)
            : sortBuilder.Ascending(x => x.Occurred).Ascending(x => x.Created);

        var documents = await _nonGroupExpensesCollection
            .Find(filter)
            .Sort(sort)
            .Limit(pageSize)
            .ToListAsync(ct);

        var results = documents.Select(d => (NonGroupExpense)Mapper.ToEntity(d)).ToList();

        if (direction == PaginationDirection.Newer)
        {
            results.Reverse();
        }

        return results;
    }

    public async Task<List<string>> GetNonGroupUserIdsByUserId(string userId, CancellationToken ct)
    {
        var filterBuilder = Builders<NonGroupExpenseMongoDbDocument>.Filter;
        var sortBuilder = Builders<NonGroupExpenseMongoDbDocument>.Sort;

        var filter = filterBuilder.Or(
            filterBuilder.ElemMatch(x => x.Shares, s => s.UserId == userId),
            filterBuilder.ElemMatch(x => x.Payments, p => p.UserId == userId));

        var sort = sortBuilder.Descending(x => x.Created);

        var documents = await _nonGroupExpensesCollection
            .Find(filter)
            .Sort(sort)
            .ToListAsync(ct);

        var payerIds = documents.SelectMany(x => x.Payments.Select(p => p.UserId));
        var participantIds = documents.SelectMany(x => x.Shares.Select(s => s.UserId));

        return payerIds.Concat(participantIds).Distinct().ToList();
    }
}