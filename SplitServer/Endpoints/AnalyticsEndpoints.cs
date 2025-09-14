using MediatR;
using SplitServer.Extensions;
using SplitServer.Queries;

namespace SplitServer.Endpoints;

public static class AnalyticsEndpoints
{
    public static void MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/spendings-chart", GetSpendingsChart);
    }

    private static async Task<IResult> GetSpendingsChart(
        string granularity,
        DateTime startDate,
        DateTime endDate,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new GetSpendingsChartQuery
        {
            UserId = httpContext.GetUserId(),
            Granularity = granularity,
            StartDate = startDate,
            EndDate = endDate
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }
}