namespace SplitServer.Responses;

public class SearchUsersToInviteResponse
{
    public required List<SearchUsersToInviteResponseItem> Users { get; init; }
    public required string? Next { get; init; }
}