namespace SplitServer.Repositories.Mappers;

public class PassThroughMapper<TEntity> : IMapper<TEntity, TEntity>
{
    public TEntity ToEntity(TEntity document)
    {
        return document;
    }

    public TEntity ToDocument(TEntity entity)
    {
        return entity;
    }
}