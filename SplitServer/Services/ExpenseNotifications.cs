namespace SplitServer.Services;

public static class ExpenseNotifications
{
    /// <summary>
    /// How an expense is named in a notification: its description in quotes with the amount after
    /// it, or the amount alone when it has no description. A description is optional, and a quoted
    /// blank told the reader nothing.
    /// </summary>
    public static string Describe(string? description, decimal amount, string currency)
    {
        var money = $"{amount} {currency}";

        return string.IsNullOrWhiteSpace(description) ? money : $"\"{description.Trim()}\" ({money})";
    }
}
