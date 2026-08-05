using MongoDB.Bson.Serialization.Attributes;
using SplitServer.Models;
using SplitServer.Requests;

namespace SplitServer.Repositories.Implementations.Models;

[BsonDiscriminator(Required = true)]
[BsonKnownTypes(typeof(GroupRecurringExpenseMongoDbDocument))]
[BsonKnownTypes(typeof(NonGroupRecurringExpenseMongoDbDocument))]
[BsonKnownTypes(typeof(PersonalRecurringExpenseMongoDbDocument))]
public record RecurringExpenseMongoDbDocument : EntityBase
{
    public required string UserId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string Description { get; init; }
    public required MongoDbLocation? Location { get; init; }
    public required List<LabelRequestItem> Labels { get; init; }
    public required RecurrenceSchedule Schedule { get; init; }
    public required string TimeZoneId { get; init; }
    public required DateTime AnchorDate { get; init; }
    public required DateTime NextOccurrence { get; init; }
    public required bool IsPaused { get; init; }
    public required string? LastExpenseId { get; init; }
    public required DateTime? LastRunAt { get; init; }
    public required string? LastError { get; init; }
}

[BsonDiscriminator("group")]
public record GroupRecurringExpenseMongoDbDocument : RecurringExpenseMongoDbDocument
{
    public required string GroupId { get; init; }
    public required List<GroupPayment> Payments { get; init; }
    public required List<GroupShare> Shares { get; init; }
}

[BsonDiscriminator("non_group")]
public record NonGroupRecurringExpenseMongoDbDocument : RecurringExpenseMongoDbDocument
{
    public required List<Payment> Payments { get; init; }
    public required List<Share> Shares { get; init; }
}

[BsonDiscriminator("personal")]
public record PersonalRecurringExpenseMongoDbDocument : RecurringExpenseMongoDbDocument;
