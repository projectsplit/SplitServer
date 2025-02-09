namespace SplitServer.Models;

public record Invitation : EntityBase
{
    public required string FromId { get; init; }
    public required string ToId { get; init; }
    public required string GroupId { get; init; }
    public required string? GuestId { get; init; }
}