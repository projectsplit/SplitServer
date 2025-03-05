namespace SplitServer.Responses;

public class AuthenticationResponse
{
    public required string RefreshToken { get; init; }
    public required string AccessToken { get; init; }
}