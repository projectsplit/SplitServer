namespace SplitServer.Commands;

public class CreateManyTransfersItem
{
    /// <summary>
    /// Picked by the app, one per transfer, when it may have to send a settle-up more than once.
    /// See <see cref="Services.ClientIds"/>. Left out, the server picks the id.
    /// </summary>
    public string? TransferId { get; init; }

    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string SenderId { get; init; }
    public required string ReceiverId { get; init; }
    public required string Description { get; init; }
    public required DateTime? Occurred { get; init; }
}