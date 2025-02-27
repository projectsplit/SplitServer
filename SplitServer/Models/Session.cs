namespace SplitServer.Models;

public record Session : EntityBase
{
    public required string UserId { get; init; }
    public required string RefreshToken { get; init; }
}