using SplitServer.Services.RiskEngine.Models;

namespace SplitServer.Models;

public record CalculatedWealth : EntityBase
{
    public required string UserId { get; init; }
    public required string RunId { get; init; }
    public required double StartingWealth { get; init; }
    public required EconomyInfo Economy { get; init; }
    public required SimulationSummary Summary { get; init; }
    public required List<ScenarioRow> Scenarios { get; init; }
    public required int NSims { get; init; }
    public double? RealizedCorrelation { get; init; }
}
