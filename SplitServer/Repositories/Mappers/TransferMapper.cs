using SplitServer.Models;
using SplitServer.Repositories.Implementations.Extensions;
using SplitServer.Repositories.Implementations.Models;

namespace SplitServer.Repositories.Mappers;

public class TransferMapper : IMapper<Transfer, TransferMongoDbDocument>
{
    public Transfer ToEntity(TransferMongoDbDocument document)
    {
        return document switch
        {
            GroupTransferMongoDbDocument d => new GroupTransfer
            {
                Id = d.Id,
                Created = d.Created,
                Updated = d.Updated,
                Occurred = d.Occurred,
                CreatorId = d.CreatorId,
                Amount = d.Amount,
                Description = d.Description,
                Currency = d.Currency,
                ReceiverId = d.ReceiverId,
                SenderId = d.SenderId,
                GroupId = d.GroupId,

            },
            NonGroupTransferMongoDbDocument d => new NonGroupTransfer
            {
                Id = d.Id,
                Created = d.Created,
                Updated = d.Updated,
                Occurred = d.Occurred,
                CreatorId = d.CreatorId,
                Amount = d.Amount,
                Description = d.Description,
                Currency = d.Currency,
                ReceiverId = d.ReceiverId,
                SenderId = d.SenderId,
            },
            _ => throw new NotSupportedException($"Mapping for document type '{document.GetType().Name}' is not implemented.")
        };
    }

    public TransferMongoDbDocument ToDocument(Transfer entity)
    {
        return entity switch
        {
            GroupTransfer t => new GroupTransferMongoDbDocument
            {
                Id = t.Id,
                Created = t.Created,
                Updated = t.Updated,
                Occurred = t.Occurred,
                CreatorId = t.CreatorId,
                Amount = t.Amount,
                Description = t.Description,
                Currency = t.Currency,
                GroupId = t.GroupId,
                ReceiverId = t.ReceiverId,
                SenderId = t.SenderId,
            },
            NonGroupTransfer e => new NonGroupTransferMongoDbDocument
            {
                Id = e.Id,
                Created = e.Created,
                Updated = e.Updated,
                Occurred = e.Occurred,
                CreatorId = e.CreatorId,
                Amount = e.Amount,
                Description = e.Description,
                Currency = e.Currency,
                ReceiverId = e.ReceiverId,
                SenderId = e.SenderId,

            },
            _ => throw new NotSupportedException($"Mapping for entity type '{entity.GetType().Name}' is not implemented.")
        };
    }
}