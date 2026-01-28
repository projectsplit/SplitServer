using SplitServer.Models;

namespace SplitServer.Responses;

public record NonGroupExpenseResponseItem
{
    public required string Id { get; init; }
    public required DateTime Created { get; init; }
    public required DateTime Updated { get; init; }
    public required string CreatorId { get; init; }
    public decimal Amount { get; init; }
    public required DateTime Occurred { get; init; }
    public required string Description { get; init; }
    public required string Currency { get; init; }
    public required List<GetNonGroupPaymentItem> Payments { get; init; }
    public required List<GetNonGroupShareItem> Shares { get; init; }
    public required List<Label> Labels { get; init; }
    public required Location? Location { get; init; }
}