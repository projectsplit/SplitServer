using MediatR;
using SplitServer.Commands;
using SplitServer.Extensions;
using SplitServer.Queries;
using SplitServer.Requests;

namespace SplitServer.Endpoints;

public static class PlusEndpoints
{
    public static void MapPlusEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/offer", GetPlusOfferHandler);
        app.MapPost("/purchase", RegisterPurchaseHandler);
    }

    private static async Task<IResult> GetPlusOfferHandler(
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetPlusOfferQuery(), ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> RegisterPurchaseHandler(
        RegisterPlusPurchaseRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new RegisterPlusPurchaseCommand
        {
            UserId = httpContext.GetUserId(),
            PurchaseToken = request.PurchaseToken,
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }
}
