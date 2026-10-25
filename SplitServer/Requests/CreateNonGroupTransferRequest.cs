namespace SplitServer.Requests;

public class CreateNonGroupTransferRequest
{
    /// <summary>
    /// Picked by the app when it may have to send this transfer more than once, so the server can
    /// recognise a resend instead of storing it again. Left out, the server picks the id.
    /// </summary>
    public string? TransferId { get; init; }

    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string Description { get; init; }
    public required DateTime? Occurred { get; init; }
    public required string SenderId { get; init; }
    public required string ReceiverId { get; init; }
}