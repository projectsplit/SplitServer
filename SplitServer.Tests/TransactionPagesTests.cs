using SplitServer.Queries.Models;
using SplitServer.Services;

namespace SplitServer.Tests;

/// <summary>
/// A list opened on one item has to grow both ways without a gap or a repeat: down through the older
/// items, and up through the newer ones until it reaches the top. The fetch here filters and orders
/// the way the repositories do, so these walk a list of 25 items the way the app scrolls it.
/// </summary>
public class TransactionPagesTests
{
    private record Item(int Number, DateTime Occurred, DateTime Created);

    private record Cursor(DateTime Occurred, DateTime Created, bool IsJumpTo) : IPageCursor;

    private const int PageSize = 10;

    private static readonly DateTime Start = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    // Number 25 is the newest. 12 and 13 share an occurred time, so the created time breaks the tie.
    private static readonly List<Item> All = Enumerable.Range(1, 25)
        .Select(n => new Item(n, Start.AddDays(n == 13 ? 12 : n), Start.AddMinutes(n)))
        .ToList();

    private static Task<List<Item>> Fetch(
        int limit,
        DateTime? occurred,
        DateTime? created,
        PaginationDirection direction,
        bool inclusive)
    {
        var newestFirst = All.OrderByDescending(x => x.Occurred).ThenByDescending(x => x.Created).ToList();

        if (occurred is null || created is null)
        {
            return Task.FromResult(newestFirst.Take(limit).ToList());
        }

        if (direction == PaginationDirection.Older)
        {
            return Task.FromResult(newestFirst
                .Where(x => x.Occurred < occurred || (x.Occurred == occurred && (inclusive ? x.Created <= created : x.Created < created)))
                .Take(limit)
                .ToList());
        }

        var nearestNewer = newestFirst
            .Where(x => x.Occurred > occurred || (x.Occurred == occurred && (inclusive ? x.Created >= created : x.Created > created)))
            .Reverse()
            .Take(limit)
            .Reverse()
            .ToList();

        return Task.FromResult(nearestNewer);
    }

    private static Cursor At(Item item, bool isJumpTo = false) => new(item.Occurred, item.Created, isJumpTo);

    private static Item Number(int n) => All.Single(x => x.Number == n);

    private static int[] Numbers(TransactionPage<Item> page) => page.Items.Select(x => x.Number).ToArray();

    [Fact]
    public void The_top_page_has_nothing_newer()
    {
        var page = TransactionPages.Load<Item>(PageSize, null, null, Fetch).Result;

        Assert.Equal([25, 24, 23, 22, 21, 20, 19, 18, 17, 16], Numbers(page));
        Assert.False(page.HasMoreNewer);
        Assert.True(page.HasMoreOlder);
    }

    [Fact]
    public void A_jump_centres_the_page_on_the_target()
    {
        var page = TransactionPages.Load<Item>(PageSize, At(Number(10), isJumpTo: true), null, Fetch).Result;

        Assert.Equal([14, 13, 12, 11, 10, 9, 8, 7, 6, 5], Numbers(page));
        Assert.True(page.HasMoreNewer);
        Assert.True(page.HasMoreOlder);
    }

    [Fact]
    public void A_jump_near_the_top_fills_the_page_from_below()
    {
        var page = TransactionPages.Load<Item>(PageSize, At(Number(24), isJumpTo: true), null, Fetch).Result;

        Assert.Equal([25, 24, 23, 22, 21, 20, 19, 18, 17, 16], Numbers(page));
        Assert.False(page.HasMoreNewer);
    }

    [Fact]
    public void Scrolling_up_from_a_jump_reaches_the_top_with_nothing_missing_or_repeated()
    {
        var page = TransactionPages.Load<Item>(PageSize, At(Number(6), isJumpTo: true), null, Fetch).Result;
        var seen = Numbers(page).ToList();

        while (page.HasMoreNewer)
        {
            page = TransactionPages.Load<Item>(PageSize, null, At(page.Items.First()), Fetch).Result;
            seen.InsertRange(0, Numbers(page));
        }

        Assert.Equal(Enumerable.Range(1, 25).Reverse().Skip(25 - seen.Count), seen);
        Assert.Equal(25, seen.Max());
        Assert.Equal(seen.Count, seen.Distinct().Count());
    }

    [Fact]
    public void Scrolling_down_from_a_jump_reaches_the_end_with_nothing_missing_or_repeated()
    {
        var page = TransactionPages.Load<Item>(PageSize, At(Number(20), isJumpTo: true), null, Fetch).Result;
        var seen = Numbers(page).ToList();

        while (page.HasMoreOlder)
        {
            page = TransactionPages.Load<Item>(PageSize, At(page.Items.Last()), null, Fetch).Result;
            seen.AddRange(Numbers(page));
        }

        Assert.Equal(1, seen.Min());
        Assert.Equal(seen.OrderByDescending(x => x), seen);
        Assert.Equal(seen.Count, seen.Distinct().Count());
    }

    [Fact]
    public void Nothing_newer_than_the_top_is_an_empty_page_offering_no_cursor()
    {
        var page = TransactionPages.Load<Item>(PageSize, null, At(Number(25)), Fetch).Result;

        Assert.Empty(page.Items);
        Assert.False(page.HasMoreNewer);
        Assert.False(page.HasMoreOlder);
    }
}
