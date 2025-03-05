namespace SplitServer.Responses;

public class GetGroupResponseMemberItem
{
    public required string Id { get; init; }
    public required string UserId { get; init; }
    public required string Name { get; init; }
    public required DateTime Joined { get; init; }
}