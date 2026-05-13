namespace SplitServer.Responses;

public class GroupTransferResponseItem
{
    public required string Id { get; init; }
    public required DateTime Created { get; init; }
    public required DateTime Updated { get; init; }
    public required string GroupId { get; init; }
    public required DateTime Occurred { get; init; }
    public required string CreatorId { get; init; }
    public decimal Amount { get; init; }
    public required string Description { get; init; }
    public required string Currency { get; init; }
    public required string SenderId { get; init; }
    public required string ReceiverId { get; init; }
}