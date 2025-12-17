namespace SplitServer.Models;

public record UserLabel : EntityBase
{
    public required string UserId { get; init; }
    public required string Text { get; init; }
    public required string Color { get; init; }
}