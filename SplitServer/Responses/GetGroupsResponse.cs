namespace SplitServer.Responses;

public class GetGroupsResponse
{
    public required List<GetGroupsResponseItem> Groups { get; init; }
    public required string? Next { get; init; }
}