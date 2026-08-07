using MediatR;
using Serilog;
using SplitServer.Commands;

namespace SplitServer.Endpoints;

public static class StripeWebhookEndpoints
{
    /// <summary>
    /// Mapped outside the authorised groups on purpose: Stripe calls this, not a signed-in browser,
    /// and it carries no token. The signature check inside the handler is what stands in for auth,
    /// so nothing here may act on the body before that check has passed.
    /// </summary>
    public static void MapStripeWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/webhook", StripeWebhookHandler);
    }

    private static async Task<IResult> StripeWebhookHandler(
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        // Read as raw text. The signature covers the exact bytes Stripe sent, so binding a typed
        // model and re-serialising it would fail verification even for a genuine call.
        httpContext.Request.EnableBuffering();
        httpContext.Request.Body.Seek(0, SeekOrigin.Begin);

        using var reader = new StreamReader(httpContext.Request.Body, leaveOpen: true);
        var payload = await reader.ReadToEndAsync(ct);

        var command = new ProcessStripeWebhookCommand
        {
            Payload = payload,
            SignatureHeader = httpContext.Request.Headers["Stripe-Signature"].FirstOrDefault(),
        };

        var result = await mediator.Send(command, ct);

        if (result.IsFailure)
        {
            // 400 tells Stripe not to bother retrying. Reserved for a body this endpoint will never
            // accept — a bad signature above all — because a retry cannot fix it. Anything that
            // could succeed on a second attempt throws instead and comes back as a 500, which Stripe
            // does retry.
            Log.Warning("Rejected a Stripe webhook: {Error}", result.Error);

            return Results.BadRequest(result.Error);
        }

        return Results.Ok();
    }
}
