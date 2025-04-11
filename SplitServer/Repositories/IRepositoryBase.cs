using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface IRepositoryBase<TEntity> where TEntity : EntityBase
{
    Task<Maybe<TEntity>> GetById(string id, CancellationToken ct);

    Task<IList<TEntity>> GetByIds(IList<string> ids, CancellationToken ct);

    Task<Result> Insert(TEntity entity, CancellationToken ct);

    Task<Result> InsertMany(IList<TEntity> entities, CancellationToken ct);

    Task<Result> Upsert(TEntity entity, CancellationToken ct);

    Task<Result> Delete(string id, CancellationToken ct);

    Task<Result> Update(TEntity updatedEntity, CancellationToken ct);
}