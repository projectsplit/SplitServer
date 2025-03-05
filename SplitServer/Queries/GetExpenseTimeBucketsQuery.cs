using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetExpenseTimeBucketsQuery : IRequest<Result<GetExpenseTimeBucketsResponse>>
{
    public required string UserId { get; init; }
    public required DateTime StartDate { get; init; }
    public required DateTime EndDate { get; init; }
    public required int BucketDurationInSeconds { get; init; }
}