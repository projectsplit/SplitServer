namespace SplitServer.Responses;

public class GetGroupResponseGuestItem
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required DateTime Joined { get; init; }
    public required bool CanBeRemoved { get; init; }
}