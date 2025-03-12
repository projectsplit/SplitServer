namespace SplitServer.Commands;

public class CreateManyTransfersItem
{
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string SenderId { get; init; }
    public required string ReceiverId { get; init; }
    public required string Description { get; init; }
    public required DateTime? Occurred { get; init; }
}