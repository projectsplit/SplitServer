using MediatR;
using SplitServer.Commands;
using SplitServer.Extensions;
using SplitServer.Queries;
using SplitServer.Requests;

namespace SplitServer.Endpoints;

public static class DonationEndpoints
{
    public static void MapDonationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/prompt", GetDonationPromptHandler);
        app.MapPost("/prompt/shown", RecordDonationPromptShownHandler);
        app.MapPost("/prompt/dismiss", DismissDonationPromptHandler);
        app.MapPost("/checkout-session", CreateCheckoutSessionHandler);
    }

    private static async Task<IResult> GetDonationPromptHandler(
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new GetDonationPromptQuery
        {
            UserId = httpContext.GetUserId(),
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> RecordDonationPromptShownHandler(
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new RecordDonationPromptShownCommand
        {
            UserId = httpContext.GetUserId(),
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> DismissDonationPromptHandler(
        DismissDonationPromptRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new DismissDonationPromptCommand
        {
            UserId = httpContext.GetUserId(),
            OptOut = request.OptOut,
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> CreateCheckoutSessionHandler(
        CreateDonationCheckoutSessionRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new CreateDonationCheckoutSessionCommand
        {
            UserId = httpContext.GetUserId(),
            AmountMinor = request.AmountMinor,
            Monthly = request.Monthly,
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }
}
