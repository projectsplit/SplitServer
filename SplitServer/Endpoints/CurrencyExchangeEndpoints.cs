using MediatR;
using SplitServer.Commands;
using SplitServer.Queries;
using SplitServer.Requests;
using SplitServer.Services.Auth;

namespace SplitServer.Endpoints;

public static class CurrencyExchangeEndpoints
{
    public static void MapCurrencyExchangeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/store-historical", StoreHistoricalRatesHandler).AllowAnonymous();
        app.MapGet("/latest", GetLatestRatesHandler);
    }

    private static async Task<IResult> StoreHistoricalRatesHandler(
        StoreHistoricalRatesRequest? request,
        AuthService authService,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (authService.ValidateApiKey(httpContext).IsFailure)
        {
            return Results.Unauthorized();
        }

        var query = new StoreHistoricalRatesCommand
        {
            Date = request?.Date
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> GetLatestRatesHandler(
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new GetLatestCurrencyExchangeRatesQuery();

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }
}