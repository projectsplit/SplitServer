namespace SplitServer.Responses;

public class SearchAllUsersResponse
{
    public required List<SearchUsersResponseItem> Users { get; init; }
    public required string? Next { get; init; }
}