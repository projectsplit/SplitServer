namespace SplitServer.Models;

public class NonGroupDebt
{
    public required string Debtor { get; init; }
    public required string DebtorName { get; init; }
    public required string Creditor { get; init; }
    public required string CreditorName { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
}