using CSharpFunctionalExtensions;
using MongoDB.Driver;
using MongoDB.Driver.Search;
using SplitServer.Models;
using SplitServer.Repositories.Mappers;

namespace SplitServer.Repositories.Implementations;

public class MongoDbRepositoryBase<TEntity, TDocument> : IRepositoryBase<TEntity>
    where TEntity : EntityBase
    where TDocument : EntityBase
{
    protected readonly IMapper<TEntity, TDocument> Mapper;
    protected readonly IMongoCollection<TDocument> Collection;

    protected MongoDbRepositoryBase(
        IMongoConnection mongoConnection,
        string collectionName,
        IMapper<TEntity, TDocument> mapper)
    {
        Mapper = mapper;
        Collection = mongoConnection.GetDatabase().GetCollection<TDocument>(collectionName);
    }

    public async Task<Maybe<TEntity>> GetById(string id, CancellationToken ct, bool includeDeleted = false)
    {
        var idQuery = FilterBuilder.Eq(x => x.Id, id);

        var filter = includeDeleted
            ? idQuery
            : FilterBuilder.And(
                idQuery,
                FilterBuilder.Eq(x => x.IsDeleted, false));

        var document = await Collection
            .Find(filter)
            .SingleOrDefaultAsync(cancellationToken: ct);

        return document is not null
            ? Mapper.ToEntity(document)
            : Maybe<TEntity>.None;
    }

    public async Task<IList<TEntity>> GetByIds(IList<string> ids, CancellationToken ct, bool includeDeleted = false)
    {
        var idsQuery = FilterBuilder.In(x => x.Id, ids);

        var filter = includeDeleted
            ? idsQuery
            : FilterBuilder.And(
                idsQuery,
                FilterBuilder.Eq(x => x.IsDeleted, false));

        var documents = await Collection
            .Find(filter)
            .ToListAsync(cancellationToken: ct);

        return documents.Select(Mapper.ToEntity).ToList();
    }

    public async Task<Result> Upsert(TEntity entity, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.Id, entity.Id);

        var result = await Collection.ReplaceOneAsync(
            filter,
            Mapper.ToDocument(entity),
            new ReplaceOptions { IsUpsert = true },
            ct);

        return result.IsAcknowledged ? Result.Success() : Result.Failure("Upsert failed");
    }

    public async Task<Result> Delete(string id, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.Id, id);

        var result = await Collection.DeleteOneAsync(filter, ct);

        return result.IsAcknowledged ? Result.Success() : Result.Failure("Delete failed");
    }

    public async Task<Result> SoftDelete(string id, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.Id, id);
        var update = UpdateBuilder.Set(x => x.IsDeleted, true);

        var result = await Collection.UpdateOneAsync(filter, update, null, ct);

        return result.IsAcknowledged ? Result.Success() : Result.Failure("Delete failed");
    }

    public async Task<Result> Update(TEntity entity, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.Id, entity.Id);

        var result = await Collection.ReplaceOneAsync(
            filter,
            Mapper.ToDocument(entity),
            new ReplaceOptions { IsUpsert = false },
            ct);

        return result.MatchedCount > 0 ? Result.Success() : Result.Failure("Update failed");
    }

    public async Task<Result> Insert(TEntity entity, CancellationToken ct)
    {
        return await Result.Try(
            async () => await Collection.InsertOneAsync(Mapper.ToDocument(entity), cancellationToken: ct),
            ex => $"Insert failed {ex.Message}");
    }

    protected static readonly FilterDefinitionBuilder<TDocument> FilterBuilder = Builders<TDocument>.Filter;
    protected static readonly UpdateDefinitionBuilder<TDocument> UpdateBuilder = Builders<TDocument>.Update;
    protected static readonly SortDefinitionBuilder<TDocument> SortBuilder = Builders<TDocument>.Sort;
    protected static readonly SearchDefinitionBuilder<TDocument> SearchBuilder = Builders<TDocument>.Search;
    protected static readonly EmptyPipelineDefinition<TDocument> PipelineBuilder = new();
}