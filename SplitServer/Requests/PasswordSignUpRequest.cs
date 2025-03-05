namespace SplitServer.Requests;

public class PasswordSignUpRequest
{
    public required string Password { get; init; }
    public required string Username { get; init; }
}