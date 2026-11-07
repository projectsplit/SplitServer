using System.Text;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using SplitServer.Models;
using SplitServer.Queries.Models;
using SplitServer.Services;

namespace SplitServer.Tests;

/// <summary>
/// The transfer counterpart of <see cref="ExpenseLinksTests"/>: the client marks the transfer whose
/// times in the list match the link's as text, so the link must carry them as Mongo serves them.
/// </summary>
public class TransferLinksTests
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
        new DateTime(2026, 11, 7, 9, 33, 41, 123, DateTimeKind.Utc).AddTicks(4567);

    private static readonly DateTime Occurred =
        new DateTime(2026, 11, 7, 12, 33, 0, 456, DateTimeKind.Local).AddTicks(89);

    private static GroupTransfer GroupTransfer() => new()
    {
        Id = "transfer-1",
        Created = Created,
        Updated = Created,
        Occurred = Occurred,
        GroupId = "group-1",
        CreatorId = "member-1",
        SenderId = "member-1",
        ReceiverId = "member-2",
        Amount = 12.5m,
        Description = "",
        Currency = "EUR",
    };

    private static NonGroupTransfer NonGroupTransfer() => new()
    {
        Id = "transfer-2",
        Created = Created,
        Updated = Created,
        Occurred = Occurred,
        CreatorId = "user-1",
        SenderId = "user-1",
        ReceiverId = "user-2",
        Amount = 7m,
        Description = "",
        Currency = "EUR",
    };

    [Fact]
    public void The_link_carries_the_times_the_list_will_show_for_the_transfer()
    {
        var url = TransferLinks.JumpTo(GroupTransfer());

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(ReadToken(url)));
        var token = JsonDocument.Parse(json).RootElement;

        Assert.StartsWith("/shared/group-1/transfers" + JumpToParameter, url);
        Assert.Equal(AsListedByTheApi(RoundTripThroughMongo(Created)), token.GetProperty("Created").GetString());
        Assert.Equal(AsListedByTheApi(RoundTripThroughMongo(Occurred)), token.GetProperty("Occurred").GetString());
    }

    [Fact]
    public void The_link_asks_for_the_page_around_the_transfer_rather_than_after_it()
    {
        var details = Next.Parse<NextTransferPageDetails>(ReadToken(TransferLinks.JumpTo(GroupTransfer())))!;

        Assert.True(details.IsJumpTo);
    }

    [Fact]
    public void A_quick_split_transfer_links_to_the_quick_split_list()
    {
        Assert.StartsWith("/shared/nongroup/transfers" + JumpToParameter, TransferLinks.JumpTo(NonGroupTransfer()));
    }
}
