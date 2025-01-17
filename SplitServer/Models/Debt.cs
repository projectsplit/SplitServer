namespace SplitServer.Models;

public record Debt
{
    public required string Debtor { get; init; }
    
    public required string Creditor { get; init; }
    
    public required decimal Amount { get; init; }

    public required string Currency { get; init; }
}