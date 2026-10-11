using System.Text;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using SplitServer.Models;
using SplitServer.Queries.Models;
using SplitServer.Services;

namespace SplitServer.Tests;

/// <summary>
/// A notification's link names its expense by two times, and the client marks the expense whose
/// times in the list match them as text. The link is built from the expense still in memory, while
/// the list serves it as read back from Mongo, so any difference in precision or kind between the
/// two would quietly leave nothing marked.
/// </summary>
public class ExpenseLinksTests
{
    private const string JumpToParameter = "?jumpTo=";

    private class StoredTime
    {
        public DateTime Value { get; set; }
    }

    private static DateTime RoundTripThroughMongo(DateTime value) =>
        BsonSerializer.Deserialize<StoredTime>(new StoredTime { Value = value }.ToBson()).Value;

    private static string AsListedByTheApi(DateTime value) =>
        JsonSerializer.Deserialize<string>(
            JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)))!;

    private static string ReadToken(string url)
    {
        var start = url.IndexOf(JumpToParameter, StringComparison.Ordinal) + JumpToParameter.Length;

        return Uri.UnescapeDataString(url[start..]);
    }

    // Finer than a millisecond and not in UTC: both are lost on the way into Mongo.
    private static readonly DateTime Created =
        new DateTime(2026, 10, 11, 9, 33, 41, 123, DateTimeKind.Utc).AddTicks(4567);

    private static readonly DateTime Occurred =
        new DateTime(2026, 10, 11, 12, 33, 0, 456, DateTimeKind.Local).AddTicks(89);

    private static GroupExpense GroupExpense() => new()
    {
        Id = "expense-1",
        Created = Created,
        Updated = Created,
        Occurred = Occurred,
        GroupId = "group-1",
        CreatorId = "member-1",
        Amount = 12.5m,
        Description = "dinner",
        Currency = "EUR",
        Location = null,
        Labels = [],
        Payments = [],
        Shares = []
    };

    [Fact]
    public void The_link_carries_the_times_the_list_will_show_for_the_expense()
    {
        var url = ExpenseLinks.JumpTo(GroupExpense());

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(ReadToken(url)));
        var token = JsonDocument.Parse(json).RootElement;

        Assert.StartsWith("/shared/group-1/expenses" + JumpToParameter, url);
        Assert.Equal(AsListedByTheApi(RoundTripThroughMongo(Created)), token.GetProperty("Created").GetString());
        Assert.Equal(AsListedByTheApi(RoundTripThroughMongo(Occurred)), token.GetProperty("Occurred").GetString());
    }

    [Fact]
    public void The_link_asks_for_the_page_around_the_expense_rather_than_after_it()
    {
        // Read as an ordinary next-page token, the same times would start the list just past the
        // expense and leave it out.
        var details = Next.Parse<NextExpensePageDetails>(ReadToken(ExpenseLinks.JumpTo(GroupExpense())))!;

        Assert.True(details.IsJumpTo);
    }
}
