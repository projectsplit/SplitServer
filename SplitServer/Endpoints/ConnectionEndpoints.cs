using MediatR;
using SplitServer.Commands;
using SplitServer.Extensions;
using SplitServer.Queries;
using SplitServer.Requests;

namespace SplitServer.Endpoints;

public static class ConnectionEndpoints
{
    public static void MapConnectionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/requests", GetConnectionRequestsHandler);
        app.MapGet("/statuses", GetConnectionStatusesHandler);
        app.MapPost("/request", SendConnectionRequestHandler);
        app.MapPost("/accept", AcceptConnectionRequestHandler);
        app.MapPost("/decline", DeclineConnectionRequestHandler);
    }

    private static async Task<IResult> GetConnectionRequestsHandler(
        int pageSize,
        string? next,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new GetConnectionRequestsQuery
        {
            UserId = httpContext.GetUserId(),
            PageSize = pageSize,
            Next = next,
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> GetConnectionStatusesHandler(
        string[] userIds,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new GetConnectionStatusesQuery
        {
            UserId = httpContext.GetUserId(),
            UserIds = userIds,
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> SendConnectionRequestHandler(
        SendConnectionRequestRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new SendConnectionRequestCommand
        {
            UserId = httpContext.GetUserId(),
            ReceiverId = request.ReceiverId,
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> AcceptConnectionRequestHandler(
        AcceptConnectionRequestRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new AcceptConnectionRequestCommand
        {
            UserId = httpContext.GetUserId(),
            ConnectionId = request.ConnectionId,
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> DeclineConnectionRequestHandler(
        DeclineConnectionRequestRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new DeclineConnectionRequestCommand
        {
            UserId = httpContext.GetUserId(),
            ConnectionId = request.ConnectionId,
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }
}
