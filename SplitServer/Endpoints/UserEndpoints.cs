using MediatR;
using SplitServer.Commands;
using SplitServer.Extensions;
using SplitServer.Queries;
using SplitServer.Requests;

namespace SplitServer.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/me", GetAuthenticatedUserHandler);
        app.MapGet("/expense-time-buckets", GetExpenseTimeBucketsHandler);
        app.MapPut("/activity/last-viewed-notification", SetLastViewedNotificationTimestampHandler);
        app.MapPut("/activity/recent-group", SetRecentGroupHandler);
        app.MapPut("/preferences/time-zone", SetTimeZoneHandler);
        app.MapPut("/preferences/currency", SetCurrencyHandler);
        app.MapGet("/username/{username}", GetUsernameStatusHandler);
        app.MapPut("/username", EditUsernameHandler);
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

    private static async Task<IResult> SetLastViewedNotificationTimestampHandler(
        SetLastViewedNotificationTimestampRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new SetLastViewedNotificationTimestampCommand
        {
            UserId = httpContext.GetUserId(),
            Timestamp = request.Timestamp
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> SetRecentGroupHandler(
        SetRecentGroupRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new SetRecentGroupCommand
        {
            UserId = httpContext.GetUserId(),
            GroupId = request.GroupId
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> SetTimeZoneHandler(
        SetTimeZoneRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new SetTimeZoneCommand
        {
            UserId = httpContext.GetUserId(),
            TimeZone = request.TimeZone
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> SetCurrencyHandler(
        SetCurrencyRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new SetCurrencyCommand
        {
            UserId = httpContext.GetUserId(),
            Currency = request.Currency
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> EditUsernameHandler(
        EditUsernameRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new EditUsernameCommand
        {
            UserId = httpContext.GetUserId(),
            Username = request.Username
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> GetUsernameStatusHandler(
        string username,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new GetUsernameStatusQuery
        {
            UserId = httpContext.GetUserId(),
            Username = username
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }
}