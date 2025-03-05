namespace SplitServer.Responses;

public class GetAuthenticatedUserResponse
{
    public required string UserId { get; init; }
    public required string Username { get; init; }
}