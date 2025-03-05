namespace SplitServer.Responses;

public class GetGroupsWithDetailsResponse
{
    public required List<GetGroupsWithDetailsResponseItem> Groups { get; init; }
    public required string? Next { get; init; }
}