using MediatR;
using SplitServer.Commands;
using SplitServer.Dto;
using SplitServer.Extensions;
using SplitServer.Queries;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace SplitServer.Endpoints;

public static class GroupEndpoints
{
    public static void MapGroupEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/create", CreateGroupHandler);
        app.MapPost("/delete", DeleteGroupHandler);
        app.MapPost("/update", UpdateGroupHandler);
        app.MapGet("/{groupId}", GetGroupHandler);
        app.MapPost("/{groupId}/add-guest", AddGuestHandler);
        app.MapGet("/", GetGroupsHandler);
    }

    private static async Task<IResult> CreateGroupHandler(
        CreateGroupRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new CreateGroupCommand(httpContext.GetUserId(), request.Name, request.Currency);

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> DeleteGroupHandler(
        DeleteGroupRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new DeleteGroupCommand(httpContext.GetUserId(), request.GroupId);

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> UpdateGroupHandler(
        UpdateGroupRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new UpdateGroupCommand(httpContext.GetUserId(), request.GroupId, request.Name, request.Currency);

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> GetGroupHandler(
        string groupId,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new GetGroupQuery(httpContext.GetUserId(), groupId);

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> GetGroupsHandler(
        int pageSize,
        string? next,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new GetGroupsQuery(httpContext.GetUserId(), pageSize, next);

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> AddGuestHandler(
        string groupId,
        AddGuestRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new AddGuestCommand(httpContext.GetUserId(), groupId, request.GuestName);

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }
}