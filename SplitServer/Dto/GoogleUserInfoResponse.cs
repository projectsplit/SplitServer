namespace SplitServer.Dto;

public class GoogleUserInfoResponse
{
    public required string Id { get; init; }
    public required string Email { get; init; }
    public string? Name { get; init; }
}