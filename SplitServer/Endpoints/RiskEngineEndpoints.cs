using MediatR;
using SplitServer.Commands;

namespace SplitServer.Endpoints;

public static class RiskEngineEndpoints
{
    public static void MapRiskEngineEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/simulate", RunSimulationHandler);
    }


    private static async Task<IResult> RunSimulationHandler(
        IMediator mediator,
        HttpContext httpContext,
        SimulationRequest request,
        CancellationToken ct)
    {
        var query = new RunSimulationCommand
        {
            Economy = request.Economy,
            Financials = request.Financials,
            RiskToggles = request.RiskToggles,
            CustomRisks = request.CustomRisks,
            Correlations = request.Correlations
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }
}
