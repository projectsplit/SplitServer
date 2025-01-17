namespace SplitServer.Repositories.Mappers;

public interface IMapper<TEntity, TDocument>
{
    TEntity ToEntity(TDocument document);

    TDocument ToDocument(TEntity entity);
}