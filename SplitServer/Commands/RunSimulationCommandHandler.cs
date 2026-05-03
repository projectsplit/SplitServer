using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Services.RiskEngine;
using SplitServer.Services.RiskEngine.Models;

namespace SplitServer.Commands;

public class RunSimulationCommandHandler : IRequestHandler<RunSimulationCommand, Result<SimulationResponse>>
{
    private readonly RiskEngineClient _riskEngineClient;

    public RunSimulationCommandHandler(RiskEngineClient riskEngineClient)
    {
        _riskEngineClient = riskEngineClient;
    }

    public async Task<Result<SimulationResponse>> Handle(RunSimulationCommand command, CancellationToken ct)
    {
        var request = new SimulationRequest
        {
            Economy = command.Economy,
            Financials = command.Financials,
            RiskToggles = command.RiskToggles,
            CustomRisks = command.CustomRisks,
            Correlations = command.Correlations
        };

        try
        {
            var response = await _riskEngineClient.RunSimulationAsync(request);
            return Result.Success(response);
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<SimulationResponse>($"Risk engine request failed: {ex.Message}");
        }
    }
}
