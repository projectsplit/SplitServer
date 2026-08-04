namespace SplitServer.Models;

/// <summary>
/// A link between two users that lets them put each other on non-group expenses and transfers.
/// Stored once per pair: the same record starts as the sender's pending request and becomes the
/// accepted connection, so a pair can never end up with two competing rows.
/// </summary>
public record UserConnection : EntityBase
{
    public required string SenderId { get; init; }
    public required string ReceiverId { get; init; }
    public required ConnectionStatus Status { get; init; }
}
