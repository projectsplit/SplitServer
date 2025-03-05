namespace SplitServer.Responses;

public class SearchUserToInviteResponse
{
    public required List<SearchUserToInviteResponseItem> Users { get; init; }
    public required string? Next { get; init; }
}