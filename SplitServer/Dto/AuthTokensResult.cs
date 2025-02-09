namespace SplitServer.Dto;

public class AuthTokensResult
{
    public required string RefreshToken { get; init; }
    public required string AccessToken { get; init; }
}