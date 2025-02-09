using MediatR;
using SplitServer.Commands;
using SplitServer.Dto;
using SplitServer.Extensions;
using SplitServer.Services;

namespace SplitServer.Endpoints;

public static class InvitationEndpoints
{
    public static void MapInvitationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/create", CreateInvitationHandler);
        app.MapPost("/accept", AcceptInvitationHandler);
        // app.MapPost("/decline", RefreshHandler);
        // app.MapPost("/revoke", RefreshHandler);
    }

    private static async Task<IResult> CreateInvitationHandler(
        CreateInvitationRequest request,
        IMediator mediator,
        HttpContext httpContext,
        AuthService authService,
        CancellationToken ct)
    {
        var command = new CreateInvitationCommand
        {
            FromId = httpContext.GetUserId(),
            ToId = request.ToId,
            GroupId = request.GroupId
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> AcceptInvitationHandler(
        AcceptInvitationRequest request,
        IMediator mediator,
        HttpContext httpContext,
        AuthService authService,
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
}