using SplitServer.Queries;

namespace SplitServer.Responses;

public class GetExpenseTimeBucketsResponse
{
    public required List<TimeBucket> Buckets { get; init; }
}