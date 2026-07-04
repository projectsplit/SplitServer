namespace SplitServer.Models;

public record UserConnection : EntityBase
{
    public required string SenderId { get; init; }
    public required string ReceiverId { get; init; }
    public required ConnectionStatus Status { get; init; }
}
