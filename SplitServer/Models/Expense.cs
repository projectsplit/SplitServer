namespace SplitServer.Models;

public abstract record Expense : EntityBase
{
    public required DateTime Occurred { get; init; }
    public required string CreatorId { get; init; }
    public decimal Amount { get; init; }
    public required string Description { get; init; }
    public required string Currency { get; init; }
    public required Location? Location { get; init; }
}

public record GroupExpense : Expense
{
    public required string GroupId { get; init; }
    public required List<GroupPayment> Payments { get; init; }
    public required List<GroupShare> Shares { get; init; }
    public required List<string> Labels { get; init; }
}

public record NonGroupExpense : Expense
{
    public required List<Payment> Payments { get; init; }
    public required List<Share> Shares { get; init; }
    public required List<string> Labels { get; init; }
}

public record PersonalExpense : Expense
{
    public required List<string> Labels { get; init; }
}