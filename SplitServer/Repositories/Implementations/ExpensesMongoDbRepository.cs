using CSharpFunctionalExtensions;
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

    public async Task<List<Expense>> GetByGroupId(
        string groupId,
        int pageSize,
        DateTime? maxOccurred,
        DateTime? maxCreated,
        CancellationToken ct)
    {
        var paginationFilter = maxOccurred is not null && maxCreated is not null
            ? FilterBuilder.Or(
                FilterBuilder.Lt(x => x.Occurred, maxOccurred),
                FilterBuilder.And(
                    FilterBuilder.Eq(x => x.Occurred, maxOccurred),
                    FilterBuilder.Lt(x => x.Created, maxCreated)))
            : FilterBuilder.Empty;

        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.GroupId, groupId),
            paginationFilter);

        var sort = SortBuilder.Descending(x => x.Occurred).Descending(x => x.Created);

        var documents = await Collection
            .Find(filter)
            .Sort(sort)
            .Limit(pageSize)
            .ToListAsync(ct);

        return documents.Select(Mapper.ToEntity).ToList();
    }

    public async Task<List<Expense>> GetAllByGroupId(string groupId, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.GroupId, groupId);

        var documents = await Collection
            .Find(filter)
            .ToListAsync(ct);

        return documents.Select(Mapper.ToEntity).ToList();
    }

    public async Task<Dictionary<string, int>> GetLabelCounts(string groupId, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.GroupId, groupId);

        var result = await Collection
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
        var filter = FilterBuilder.Eq(x => x.GroupId, groupId);

        var result = await Collection.DeleteManyAsync(filter, null, ct);

        return result.IsAcknowledged ? Result.Success() : Result.Failure("Failed to delete group expenses");
    }

    public async Task<List<Expense>> GetAllByMemberIds(List<string> memberIds, CancellationToken ct)
    {
        var sharesFilter = FilterBuilder.In("Shares.MemberId", memberIds);
        var paymentsFilter = FilterBuilder.In("Payments.MemberId", memberIds);

        var filter = FilterBuilder.Or(sharesFilter, paymentsFilter);

        var documents = await Collection
            .Find(filter)
            .ToListAsync(ct);

        return documents.Select(Mapper.ToEntity).ToList();
    }

    public async Task<List<Expense>> GetAllByMemberIds(List<string> memberIds, DateTime startDate, DateTime endDate, CancellationToken ct)
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

        return documents.Select(Mapper.ToEntity).ToList();
    }

    public async Task<bool> ExistsInAnyExpense(string groupId, string memberId, CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.GroupId, groupId),
            FilterBuilder.Or(
                FilterBuilder.Eq("Shares.MemberId", memberId),
                FilterBuilder.Eq("Payments.MemberId", memberId)));

        return await Collection.Find(filter).Limit(1).AnyAsync(ct);
    }

    public async Task<bool> LabelIsInUse(string groupId, string labelId, CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.GroupId, groupId),
            FilterBuilder.AnyEq(x => x.Labels, labelId));

        return await Collection.Find(filter).Limit(1).AnyAsync(ct);
    }
}