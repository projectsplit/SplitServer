namespace SplitServer.Responses;

public class SearchNonGroupUsersResponse
{
    public required List<SearchUsersResponseItem> Users { get; init; }
}