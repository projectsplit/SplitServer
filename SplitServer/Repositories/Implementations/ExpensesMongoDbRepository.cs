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
        DateTime? maxOccured,
        DateTime? maxCreated,
        CancellationToken ct)
    {
        var paginationFilter = maxOccured is not null && maxCreated is not null
            ? FilterBuilder.Or(
                FilterBuilder.Lt(x => x.Occured, maxOccured),
                FilterBuilder.And(
                    FilterBuilder.Eq(x => x.Occured, maxOccured),
                    FilterBuilder.Lt(x => x.Created, maxCreated)))
            : FilterBuilder.Empty;

        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.GroupId, groupId),
            FilterBuilder.Eq(x => x.IsDeleted, false),
            paginationFilter);

        var sort = SortBuilder.Descending(x => x.Occured).Descending(x => x.Created);

        var documents = await Collection
            .Find(filter)
            .Sort(sort)
            .Limit(pageSize)
            .ToListAsync(ct);

        return documents.Select(Mapper.ToEntity).ToList();
    }

    public async Task<List<Expense>> GetAllByGroupId(string groupId, CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.GroupId, groupId),
            FilterBuilder.Eq(x => x.IsDeleted, false));

        var documents = await Collection
            .Find(filter)
            .ToListAsync(ct);

        return documents.Select(Mapper.ToEntity).ToList();
    }

    public async Task<List<LabelCount>> GetAllLabels(string groupId, CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.GroupId, groupId),
            FilterBuilder.Eq(x => x.IsDeleted, false));

        return await Collection
            .Aggregate()
            .Match(filter)
            .Unwind(x => x.Labels)
            .Group(
                x => x["Labels"],
                g => new LabelCount
                {
                    Label = g.Key.ToString()!,
                    Count = g.Count()
                })
            .SortByDescending(x => x.Count)
            .ThenBy(x => x.Label)
            .ToListAsync(ct);
    }

    public async Task<Result> SoftDeleteByGroupId(string groupId, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.GroupId, groupId);
        var update = UpdateBuilder.Set(x => x.IsDeleted, true);
        
        var result = await Collection.UpdateManyAsync(filter, update, null, ct);
        
        return result.IsAcknowledged ? Result.Success() : Result.Failure("Failed to delete group expenses"); 
    }
}