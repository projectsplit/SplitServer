namespace SplitServer.Responses;

public class GetConnectionRequestsResponse
{
    public required List<ConnectionRequestResponseItem> ConnectionRequests { get; init; }
    public required string? Next { get; init; }
}
