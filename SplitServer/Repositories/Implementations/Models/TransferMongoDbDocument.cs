using MongoDB.Bson.Serialization.Attributes;
using SplitServer.Models;

namespace SplitServer.Repositories.Implementations.Models;

[BsonDiscriminator(Required = true)]
[BsonKnownTypes(typeof(GroupTransferMongoDbDocument))]
[BsonKnownTypes(typeof(NonGroupTransferMongoDbDocument))]

public record TransferMongoDbDocument : EntityBase
{
    public required string CreatorId { get; init; }
    public required string SenderId { get; init; }
    public required string ReceiverId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string Description { get; init; }
    public required DateTime Occurred { get; init; }
}

[BsonDiscriminator("group")]

public record GroupTransferMongoDbDocument : TransferMongoDbDocument
{
    public required string GroupId { get; init; }

}

[BsonDiscriminator("non_group")]
public record NonGroupTransferMongoDbDocument : TransferMongoDbDocument
{

}