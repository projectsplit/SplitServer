using SplitServer.Queries.Models;

namespace SplitServer.Services;

/// <summary>
/// A page of a list of expenses or transfers, which is ordered newest first and read by time.
/// <para>
/// A list is normally read from the top, each page carrying on after the last item of the one
/// before. A link to one item opens it on the page around that item instead, and from there it
/// grows both ways: older items below as the person scrolls down, newer ones above as they scroll
/// up. So every page says whether there is more in each direction, and the response turns that into
/// a cursor for each — <c>next</c> from its last item, <c>previous</c> from its first.
/// </para>
/// </summary>
public static class TransactionPages
{
    /// <summary>
    /// Up to <paramref name="limit"/> items on one side of a position, newest first. Older means
    /// before it, newer after it; inclusive counts the item at the position itself.
    /// </summary>
    public delegate Task<List<T>> Fetch<T>(
        int limit,
        DateTime? occurred,
        DateTime? created,
        PaginationDirection direction,
        bool inclusive);

    public static async Task<TransactionPage<T>> Load<T>(
        int pageSize,
        IPageCursor? next,
        IPageCursor? previous,
        Fetch<T> fetch)
    {
        if (previous is not null)
        {
            // The items just newer than the top of what the person already has. Newest first, so
            // the extra one fetched to see whether there is more is the first.
            var newerItems = await fetch(pageSize + 1, previous.Occurred, previous.Created, PaginationDirection.Newer, false);

            var hasMoreNewer = newerItems.Count > pageSize;

            if (hasMoreNewer)
            {
                newerItems.RemoveAt(0);
            }

            // The item the cursor came from is below this page, so there is always more that way.
            return Page(newerItems, hasMoreNewer, hasMoreOlder: true);
        }

        if (next?.IsJumpTo == true)
        {
            // Centred on the target rather than starting at it, so the item the person was sent to
            // has context on both sides. The target itself is counted with the newer half.
            var newerTargetCount = pageSize / 2;

            var newerItems = await fetch(newerTargetCount + 1, next.Occurred, next.Created, PaginationDirection.Newer, true);

            var hasMoreNewer = newerItems.Count > newerTargetCount;

            if (hasMoreNewer)
            {
                newerItems.RemoveAt(0);
            }

            var olderNeeded = pageSize - newerItems.Count;

            var olderItems = await fetch(olderNeeded + 1, next.Occurred, next.Created, PaginationDirection.Older, false);

            var hasMoreOlder = olderItems.Count > olderNeeded;

            if (hasMoreOlder)
            {
                olderItems.RemoveAt(olderItems.Count - 1);
            }

            return Page(newerItems.Concat(olderItems).ToList(), hasMoreNewer, hasMoreOlder);
        }

        var items = await fetch(pageSize + 1, next?.Occurred, next?.Created, PaginationDirection.Older, false);

        var hasMore = items.Count > pageSize;

        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        // A page further down always has newer items above it; only the top one has none.
        return Page(items, hasMoreNewer: next is not null, hasMore);
    }

    // A cursor is built from an item, so an empty page offers none either way.
    private static TransactionPage<T> Page<T>(List<T> items, bool hasMoreNewer, bool hasMoreOlder) =>
        new(items, hasMoreNewer && items.Count > 0, hasMoreOlder && items.Count > 0);
}

public record TransactionPage<T>(List<T> Items, bool HasMoreNewer, bool HasMoreOlder);
