using MediatR;
using SplitServer.Commands;
using SplitServer.Dto;
using SplitServer.Extensions;

namespace SplitServer.Endpoints;

public static class InvitationEndpoints
{
    public static void MapInvitationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/create", CreateInvitationHandler);
        app.MapPost("/accept", AcceptInvitationHandler);
        app.MapPost("/decline", DeclineInvitationHandler);
        // app.MapPost("/revoke", RefreshHandler);
    }

    private static async Task<IResult> CreateInvitationHandler(
        CreateInvitationRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new CreateInvitationCommand
        {
            UserId = httpContext.GetUserId(),
            ToId = request.ToId,
            GroupId = request.GroupId,
            GuestId = request.GuestId,
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
            InvitationId = request.InvitationId
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }
}