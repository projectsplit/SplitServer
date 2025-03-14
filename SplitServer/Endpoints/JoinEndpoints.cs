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
        app.MapGet("/group/{groupId}", GetGroupJoinCodesHandler);
        app.MapPost("/", JoinWithCodeHandler);
        app.MapGet("/{code}", GetJoinCodeHandler);
        app.MapPost("/create", CreateJoinCodeHandler);
        app.MapPost("/revoke", RevokeJoinCodeHandler);
    }

    private static async Task<IResult> GetGroupJoinCodesHandler(
        string groupId,
        int pageSize,
        string? next,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new GetGroupJoinCodesQuery
        {
            UserId = httpContext.GetUserId(),
            GroupId = groupId,
            PageSize = pageSize,
            Next = next,
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> JoinWithCodeHandler(
        JoinWithCodeRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new JoinWithCodeCommand
        {
            UserId = httpContext.GetUserId(),
            Code = request.Code,
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> GetJoinCodeHandler(
        string code,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new GetJoinCodeQuery
        {
            UserId = httpContext.GetUserId(),
            Code = code
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> CreateJoinCodeHandler(
        CreateJoinCodeRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new CreateJoinCodeCommand
        {
            UserId = httpContext.GetUserId(),
            GroupId = request.GroupId,
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> RevokeJoinCodeHandler(
        RevokeJoinCodeRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new RevokeJoinCodeCommand
        {
            UserId = httpContext.GetUserId(),
            Code = request.Code,
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }
}