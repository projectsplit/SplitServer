using MediatR;
using SplitServer.Commands;
using SplitServer.Extensions;
using SplitServer.Queries;
using SplitServer.Requests;

namespace SplitServer.Endpoints;

public static class JoinEndpoints
{
    public static void MapJoinEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/group/{groupId}", GetGroupJoinTokensHandler);
        app.MapPost("/use", UseJoinTokenHandler);
        app.MapPost("/create", CreateJoinTokenHandler);
        app.MapPost("/revoke", RevokeJoinTokenHandler);
    }

    private static async Task<IResult> GetGroupJoinTokensHandler(
        string groupId,
        int pageSize,
        string? next,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new GetGroupJoinTokensQuery
        {
            UserId = httpContext.GetUserId(),
            GroupId = groupId,
            PageSize = pageSize,
            Next = next,
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> UseJoinTokenHandler(
        UseJoinTokenRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new UseJoinTokenCommand
        {
            UserId = httpContext.GetUserId(),
            JoinToken = request.JoinToken,
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> CreateJoinTokenHandler(
        CreateJoinTokenRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new CreateJoinTokenCommand
        {
            UserId = httpContext.GetUserId(),
            GroupId = request.GroupId,
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> RevokeJoinTokenHandler(
        RevokeJoinTokenRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new RevokeJoinTokenCommand
        {
            UserId = httpContext.GetUserId(),
            JoinToken = request.JoinToken,
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }
}