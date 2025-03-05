namespace SplitServer.Requests;

public class PasswordSignInRequest
{
    public required string Username { get; init; }
    public required string Password { get; init; }
}