
namespace SplitServer.Requests;

public class CreateTransferRequest
{
    public required string GroupId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string Description { get; init; }
    public required DateTime? Occurred { get; init; }
    public required string SenderId { get; init; }
    public required string ReceiverId { get; init; }
}