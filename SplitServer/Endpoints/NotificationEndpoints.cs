using MediatR;
using SplitServer.Commands;
using SplitServer.Extensions;
using SplitServer.Requests;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/vapid-public-key", GetVapidPublicKeyHandler);
        app.MapPost("/subscribe", SubscribeHandler);
        app.MapPost("/unsubscribe", UnsubscribeHandler);
        app.MapPut("/preference", SetPushNotificationsEnabledHandler);
    }

    private static IResult GetVapidPublicKeyHandler(PushNotificationService pushNotificationService)
    {
        return Results.Ok(new GetVapidPublicKeyResponse { PublicKey = pushNotificationService.PublicKey });
    }

    private static async Task<IResult> SubscribeHandler(
        SubscribeToPushRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new SubscribeToPushCommand
        {
            UserId = httpContext.GetUserId(),
            Endpoint = request.Endpoint,
            P256dh = request.P256dh,
            Auth = request.Auth,
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> UnsubscribeHandler(
        UnsubscribeFromPushRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new UnsubscribeFromPushCommand
        {
            UserId = httpContext.GetUserId(),
            Endpoint = request.Endpoint,
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> SetPushNotificationsEnabledHandler(
        SetPushNotificationsEnabledRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new SetPushNotificationsEnabledCommand
        {
            UserId = httpContext.GetUserId(),
            Enabled = request.Enabled,
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }
}
