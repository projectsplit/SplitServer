using MediatR;
using SplitServer.Commands;
using SplitServer.Extensions;
using SplitServer.Queries;

namespace SplitServer.Endpoints;

public static class RiskEngineEndpoints
{
    public static void MapRiskEngineEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/simulate", RunSimulationHandler);
        app.MapGet("/mostrecentsetup", GetMostRecentEngineSetupHandler);
    }

    private static async Task<IResult> RunSimulationHandler(
        IMediator mediator,
        HttpContext httpContext,
        SimulationRequest request,
        CancellationToken ct)
    {
        var command = new RunSimulationCommand
        {
            UserId = httpContext.GetUserId(),
            Economy = request.Economy,
            Financials = request.Financials,
            RiskToggles = request.RiskToggles,
            CustomRisks = request.CustomRisks,
            Correlations = request.Correlations
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> GetMostRecentEngineSetupHandler(
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new GetMostRecentEngineSetupQuery
        {
            UserId = httpContext.GetUserId()
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }
}
