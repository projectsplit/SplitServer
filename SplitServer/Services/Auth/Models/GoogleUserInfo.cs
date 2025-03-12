namespace SplitServer.Services.Auth.Models;

public class GoogleUserInfo
{
    public required string Id { get; init; }
    public required string Email { get; init; }
    public required string? Name { get; init; }
}