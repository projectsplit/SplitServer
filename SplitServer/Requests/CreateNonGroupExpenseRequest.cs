using SplitServer.Models;

namespace SplitServer.Requests;

public class CreateNonGroupExpenseRequest
{
    /// <summary>
    /// Picked by the app when it may have to send this expense more than once, so the server can
    /// recognise a resend instead of storing it again. Left out, the server picks the id.
    /// </summary>
    public string? ExpenseId { get; init; }

    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string Description { get; init; }
    public required DateTime? Occurred { get; init; }
    public required List<Payment> Payments { get; init; }
    public required List<Share> Shares { get; init; }
    public required List<LabelRequestItem> Labels { get; init; }
    public required Location? Location { get; init; }
}