using SplitServer.Queries;

namespace SplitServer.Dto;

public class GetExpenseTimeBucketsResponse
{
    public required List<TimeBucket> Buckets { get; init; }
}