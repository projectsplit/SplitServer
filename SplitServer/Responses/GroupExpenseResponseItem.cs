using SplitServer.Models;

namespace SplitServer.Responses;

public record GroupExpenseResponseItem
{
    public required string Id { get; init; }
    public required DateTime Created { get; init; }
    public required DateTime Updated { get; init; }
    public required string GroupId { get; init; }
    public required string CreatorId { get; init; }
    public decimal Amount { get; init; }
    public required DateTime Occurred { get; init; }
    public required string Description { get; init; }
    public required string Currency { get; init; }
    public required List<GroupPayment> Payments { get; init; }
    public required List<GroupShare> Shares { get; init; }
    public required List<Label> Labels { get; init; }
    public required Location? Location { get; init; }
}