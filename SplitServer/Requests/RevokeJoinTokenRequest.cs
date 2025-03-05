namespace SplitServer.Requests;

public class RevokeJoinTokenRequest
{
    public required string JoinToken { get; init; }
}