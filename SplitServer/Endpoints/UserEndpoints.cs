using MediatR;
using SplitServer.Extensions;
using SplitServer.Queries;

namespace SplitServer.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/me", GetAuthenticatedUserHandler);
        app.MapGet("/expense-time-buckets", GetExpenseTimeBucketsHandler);
    }

    private static async Task<IResult> GetAuthenticatedUserHandler(
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new GetAuthenticatedUserQuery
        {
            UserId = httpContext.GetUserId()
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> GetExpenseTimeBucketsHandler(
        DateTime startDate,
        DateTime endDate,
        int bucketDurationInSeconds,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new GetExpenseTimeBucketsQuery
        {
            UserId = httpContext.GetUserId(),
            StartDate = startDate,
            EndDate = endDate,
            BucketDurationInSeconds = bucketDurationInSeconds
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }
}