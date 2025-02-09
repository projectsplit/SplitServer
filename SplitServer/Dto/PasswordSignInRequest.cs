namespace SplitServer.Dto;

public class PasswordSignInRequest
{
    public required string Username { get; init; }
    public required string Password { get; init; }
}