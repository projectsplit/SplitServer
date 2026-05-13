using CSharpFunctionalExtensions;
using MongoDB.Driver;
using SplitServer.Models;
using SplitServer.Repositories.Mappers;

namespace SplitServer.Repositories.Implementations;

public class BudgetsMongoDbRepository : MongoDbRepositoryBase<Budget, Budget>, IBudgetsRepository
{
    public BudgetsMongoDbRepository(IMongoConnection mongoConnection) :
        base(
            mongoConnection,
            "Budgets",
            new PassThroughMapper<Budget>())
    {
    }

    public async Task<List<Budget>> GetAllByUserId(string userId, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.UserId, userId);

        var documents = await Collection
            .Find(filter)
            .ToListAsync(ct);

        return documents.Select(Mapper.ToEntity).ToList();
    }

    public async Task<Result> DeactivateAllByUserId(string userId, CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.UserId, userId),
            FilterBuilder.Eq(x => x.IsActive, true));

        var update = UpdateBuilder.Set(x => x.IsActive, false);

        var result = await Collection.UpdateManyAsync(filter, update, cancellationToken: ct);

        return result.IsAcknowledged ? Result.Success() : Result.Failure("Failed to deactivate budgets");
    }
}