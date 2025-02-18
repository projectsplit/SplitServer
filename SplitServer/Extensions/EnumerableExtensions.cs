namespace SplitServer.Extensions;

public static class EnumerableExtensions
{
    public static IEnumerable<T> CumulativeSum<T>(this IEnumerable<T> sequence) where T : struct
    {
        T sum = default;
        foreach (var item in sequence)
        {
            sum = (dynamic)sum + item;
            yield return sum;
        }
    }
}