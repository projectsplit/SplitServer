using System.Text;
using System.Text.Json;
using SplitServer.Models;
using SplitServer.Queries.Models;

namespace SplitServer.Services;

/// <summary>
/// In-app links that open an expense list centred on one expense, with it marked. This is the
/// same jump the app makes from a personal expense to the group or quick-split one it came from,
/// so a notification lands on the expense it is about rather than at the top of the list.
/// </summary>
public static class ExpenseLinks
{
    public static string JumpTo(GroupExpense expense) =>
        $"/shared/{expense.GroupId}/expenses?jumpTo={CreateJumpToken(expense)}";

    public static string JumpTo(NonGroupExpense expense) =>
        $"/shared/nongroup/expenses?jumpTo={CreateJumpToken(expense)}";

    /// <summary>
    /// The client finds the expense to mark by comparing these times, as text, with the ones the
    /// list returns. So they have to be the stored values and not the ones still in memory, which
    /// can have a finer precision and a different kind than Mongo keeps.
    /// </summary>
    private static string CreateJumpToken(Expense expense)
    {
        var details = new NextExpensePageDetails
        {
            Created = AsStored(expense.Created),
            Occurred = AsStored(expense.Occurred),
            IsJumpTo = true
        };

        var jsonString = JsonSerializer.Serialize(details);

        return Uri.EscapeDataString(Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonString)));
    }

    /// <summary>
    /// What the driver writes and reads back: UTC, the way DateTime.ToUniversalTime converts it,
    /// cut down to whole milliseconds since the epoch.
    /// </summary>
    private static DateTime AsStored(DateTime value)
    {
        var milliseconds = (value.ToUniversalTime() - DateTime.UnixEpoch).Ticks / TimeSpan.TicksPerMillisecond;

        return DateTime.UnixEpoch.AddTicks(milliseconds * TimeSpan.TicksPerMillisecond);
    }
}
