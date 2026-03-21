using SplitServer.Models;

namespace SplitServer.Responses;

public class GetInactiveBudgetsInfoResponseItem
{
    public required string Id { get; init; }
    public required decimal Amount { get; init; }
    public required string Description { get; init; }
    public required string Currency { get; init; }
    public required BudgetFrequency Frequency { get; init; }
    public required DateTime EndDate { get; init; }
    public required DateTime StartDate { get; init; }
}