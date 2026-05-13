using MongoDB.Bson.Serialization.Attributes;
using SplitServer.Models;

namespace SplitServer.Repositories.Implementations.Models;

[BsonDiscriminator(Required = true)]
[BsonKnownTypes(typeof(GroupExpenseMongoDbDocument))]
[BsonKnownTypes(typeof(NonGroupExpenseMongoDbDocument))]
[BsonKnownTypes(typeof(PersonalExpenseMongoDbDocument))]
public record ExpenseMongoDbDocument : EntityBase
{
    public required string CreatorId { get; init; }
    public decimal Amount { get; init; }
    public required DateTime Occurred { get; init; }
    public required string Description { get; init; }
    public required string Currency { get; init; }
    public required MongoDbLocation? Location { get; init; }
    public required List<string> Labels { get; init; }
}

[BsonDiscriminator("group")]
public record GroupExpenseMongoDbDocument : ExpenseMongoDbDocument
{
    public required string GroupId { get; init; }
    public required List<GroupPayment> Payments { get; init; }
    public required List<GroupShare> Shares { get; init; }
}

[BsonDiscriminator("non_group")]
public record NonGroupExpenseMongoDbDocument : ExpenseMongoDbDocument
{
    public required List<Payment> Payments { get; init; }
    public required List<Share> Shares { get; init; }
}

[BsonDiscriminator("personal")]
public record PersonalExpenseMongoDbDocument : ExpenseMongoDbDocument;