namespace SplitServer.Models;

public record User : EntityBase
{
    public required string? Email { get; init; }
    public required string? HashedPassword { get; init; }
    public required string Username { get; init; }
    public required string? GoogleId { get; init; }
    public required List<Label> Labels { get; init; }
}