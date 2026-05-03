using SplitServer.Services.RiskEngine.Models;

namespace SplitServer.Models;

public record RiskEngineSetup : EntityBase
{
    public required string UserId { get; init; }
    public required string Economy { get; init; }
    public required Financials Financials { get; init; }
    public required RiskToggles RiskToggles { get; init; }
    public required List<CustomRisk> CustomRisks { get; init; }
    public CorrelationInput? Correlations { get; init; }
}
