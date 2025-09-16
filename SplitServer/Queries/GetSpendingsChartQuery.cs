using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetSpendingsChartQuery : IRequest<Result<GetSpendingsChartResponse>>
{
    public required string UserId { get; init; }
    public required string Currency { get; init; }
    public required string Granularity { get; init; }
    public required DateTime StartDate { get; init; }
    public required DateTime EndDate { get; init; }
}
