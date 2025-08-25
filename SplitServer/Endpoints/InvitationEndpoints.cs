using MediatR;
using SplitServer.Commands;
using SplitServer.Extensions;
using SplitServer.Queries;
using SplitServer.Requests;

namespace SplitServer.Endpoints;

public static class InvitationEndpoints
{
    public static void MapInvitationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", GetUserInvitations);
        app.MapPost("/send", SendInvitationHandler);
        app.MapPost("/accept", AcceptInvitationHandler);
        app.MapPost("/decline", DeclineInvitationHandler);
        app.MapPost("/revoke", RevokeInvitationHandler);
        app.MapGet("/search-users", SearchUsersHandler);
    }

    private static async Task<IResult> SendInvitationHandler(
        SendInvitationRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new SendInvitationCommand
        {
            UserId = httpContext.GetUserId(),
            ReceiverId = request.ReceiverId,
            GroupId = request.GroupId,
            GuestId = request.GuestId,
            GuestName = request.GuestName
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> AcceptInvitationHandler(
        AcceptInvitationRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new AcceptInvitationCommand
        {
            UserId = httpContext.GetUserId(),
            InvitationId = request.InvitationId
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> DeclineInvitationHandler(
        DeclineInvitationRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new DeclineInvitationCommand
        {
            UserId = httpContext.GetUserId(),
            InvitationId = request.InvitationId
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> RevokeInvitationHandler(
        RevokeInvitationRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new RevokeInvitationCommand
        {
            UserId = httpContext.GetUserId(),
            GroupId = request.GroupId,
            ReceiverId = request.ReceiverId,
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> GetUserInvitations(
        int pageSize,
        string? next,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new GetUserInvitationsQuery
        {
            UserId = httpContext.GetUserId(),
            PageSize = pageSize,
            Next = next,
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> SearchUsersHandler(
        string groupId,
        string? keyword,
        int pageSize,
        string? next,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new SearchUserToInviteQuery
        {
            UserId = httpContext.GetUserId(),
            GroupId = groupId,
            PageSize = pageSize,
            Keyword = keyword,
            Next = next
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }
}