namespace SplitServer.Responses;

public class GetGroupsWithDetailsResponseItem
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Currency { get; init; }
    public required Dictionary<string, decimal> Details { get; init; }
    public required bool IsArchived { get; init; }
}