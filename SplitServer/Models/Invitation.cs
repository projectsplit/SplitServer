namespace SplitServer.Models;

public record Invitation : EntityBase
{
    public required string SenderId { get; init; }
    public required string ReceiverId { get; init; }
    public required string GroupId { get; init; }
    public required string? GuestId { get; init; }
    
    public required string? GuestName { get; init; }
}