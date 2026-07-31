using MongoDB.Bson.Serialization.Attributes;

namespace SplitServer.Models;

[BsonIgnoreExtraElements]
public record User : EntityBase
{
    public required string? Email { get; init; }
    public required bool EmailVerified { get; init; }
    public required string? HashedPassword { get; init; }
    public required string Username { get; init; }
    public required string? GoogleId { get; init; }
}
