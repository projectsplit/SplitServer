using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Services.RiskEngine.Models;

namespace SplitServer.Commands;

public class RunSimulationCommand : IRequest<Result<SimulationResponse>>
{
    public string Economy { get; init; } = "UK";
    public required Financials Financials { get; init; }
    public required RiskToggles RiskToggles { get; init; }
    public List<CustomRisk> CustomRisks { get; init; } = new();
    public CorrelationInput? Correlations { get; init; }
}
