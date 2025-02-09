namespace SplitServer.Models;

public record Transfer : EntityBase
{
    public required string GroupId { get; init; }
    public required string CreatorId { get; init; }
    public required string SenderId { get; init; }
    public required string ReceiverId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string Description { get; init; }
    public required DateTime Occured { get; init; }
}