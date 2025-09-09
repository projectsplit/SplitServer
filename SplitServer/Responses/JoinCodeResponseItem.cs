namespace SplitServer.Responses;

public record JoinCodeResponseItem
{
    public required string Id { get; init; }
    public required DateTime Created { get; init; }
    public required DateTime Updated { get; init; }
    public required string GroupId { get; init; }
    public required string CreatorId { get; init; }
    public required string CreatorUsername { get; init; }
    public required int TimesUsed { get; init; }
    public required int MaxUses { get; init; }
    public required DateTime Expires { get; init; }
}