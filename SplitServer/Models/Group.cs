namespace SplitServer.Models;

public record Group : EntityBase
{
    public required string OwnerId { get; init; }
    public required string Name { get; init; }
    public required string Currency { get; init; }
    public required bool IsArchived { get; init; }
    public required List<Member> Members { get; init; }
    public required List<Guest> Guests { get; init; }
    public required List<Label> Labels { get; init; }
}