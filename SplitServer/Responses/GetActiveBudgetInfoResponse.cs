using SplitServer.Models;

namespace SplitServer.Responses;

public class GetActiveBudgetInfoResponse
{
    public required string TotalAmountSpent { get; init; }
    public required string Id { get; init; }
    public required string Description { get; init; }
    public required string RemainingDays { get; init; }
    public required string AverageSpentPerDay { get; init; }
    public required string Goal { get; init; }
    public required string Currency { get; init; }
    public required BudgetFrequency Frequency  {get; init; }
    public required DateTime StartDate {get; init; }
    public required DateTime EndDate {get; init; }
    
}