using System.Text;
using System.Text.Json;

namespace SplitServer.Services;

public static class Next
{
    public static string? Create<TItem, TNext>(
        List<TItem> pageItems,
        int pageSize,
        Func<List<TItem>, TNext?> nextFunc) where TNext : class
    {
        if (pageItems.Count < pageSize)
        {
            return null;
        }

        var jsonString = JsonSerializer.Serialize(nextFunc(pageItems));

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonString));
    }

    public static TNext? Parse<TNext>(string? next) where TNext : class
    {
        if (string.IsNullOrEmpty(next))
        {
            return null;
        }

        var jsonString = Encoding.UTF8.GetString(Convert.FromBase64String(next));

        return JsonSerializer.Deserialize<TNext>(jsonString);
    }
}