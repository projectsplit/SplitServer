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
        app.MapPut("/activity/last-viewed-notification", SetLastViewedNotificationTimestampHandler);
        app.MapPut("/activity/recent-context", SetRecentContextHandler);
        app.MapPut("/preferences/time-zone", SetTimeZoneHandler);
        app.MapPut("/preferences/currency", SetCurrencyHandler);
        app.MapGet("/username/{username}", GetUsernameStatusHandler);
        app.MapPut("/username", EditUsernameHandler);
        app.MapGet("/search-non-group-expense-users", SearchNonGroupExpenseUsersHandler);
        app.MapGet("/search-non-group-transfer-users", SearchNonGroupTransferUsersHandler);
        app.MapGet("/search-all-users", SearchAllUsersHandler);
        app.MapGet("/user-labels", GetAllUserLabels);
    }

    private static async Task<IResult> GetAllUserLabels(
        IMediator mediator, 
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new GetUserLabelsQuery
        {
            UserId = httpContext.GetUserId()
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
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

    private static async Task<IResult> SetRecentContextHandler(
        SetRecentContextRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new SetRecentContextCommand
        {
            UserId = httpContext.GetUserId(),
            ContextId = request.ContextId
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

    private static async Task<IResult> SearchNonGroupExpenseUsersHandler(
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new SearchNonGroupExpenseUsersQuery
        {
            UserId = httpContext.GetUserId(),
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> SearchNonGroupTransferUsersHandler(
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new SearchNonGroupTransferUsersQuery
        {
            UserId = httpContext.GetUserId(),
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> SearchAllUsersHandler(
        string? keyword,
        int pageSize,
        string? next,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new SearchAllUsersQuery
        {
            UserId = httpContext.GetUserId(),
            PageSize = pageSize,
            Keyword = keyword,
            Next = next
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }
}