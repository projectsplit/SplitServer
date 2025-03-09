namespace SplitServer.Models;

public record JoinCode : EntityBase
{
    public required string GroupId { get; init; }
    public required string CreatorId { get; init; }
    public required int TimesUsed { get; init; }
    public required int MaxUses { get; init; }
    public required DateTime Expires { get; init; }
}