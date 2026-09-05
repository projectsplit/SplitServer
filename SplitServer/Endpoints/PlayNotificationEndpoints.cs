using MediatR;
using Serilog;
using SplitServer.Commands;

namespace SplitServer.Endpoints;

public static class PlayNotificationEndpoints
{
    /// <summary>
    /// Mapped outside the authorised groups on purpose: Google Cloud Pub/Sub calls this, not a
    /// signed-in app, and it carries no token of ours. The shared secret on the URL and the
    /// re-reading of every purchase from the Play API are what stand in for auth, so nothing here
    /// may act on the body before those have had their say.
    /// </summary>
    public static void MapPlayNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/notifications", PlayNotificationHandler);
    }

    private static async Task<IResult> PlayNotificationHandler(
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        // Read as raw text rather than bound to a model, because the interesting part is base64
        // inside the body and binding twice would mean parsing it twice.
        using var reader = new StreamReader(httpContext.Request.Body);
        var payload = await reader.ReadToEndAsync(ct);

        var command = new ProcessPlayNotificationCommand
        {
            Payload = payload,
            Secret = httpContext.Request.Query["token"].FirstOrDefault(),
        };

        var result = await mediator.Send(command, ct);

        if (result.IsFailure)
        {
            // 200 rather than 400, deliberately. Pub/Sub redelivers anything non-2xx until the
            // message expires, and every failure reachable here is one that redelivery cannot fix —
            // a bad secret, an unreadable body, another app's notification. Answering 400 would earn
            // a week of retries for each. Anything that could succeed on a second attempt throws
            // instead and comes back as a 500, which Pub/Sub does retry.
            Log.Warning("Rejected a Google Play notification: {Error}", result.Error);
        }

        return Results.Ok();
    }
}
