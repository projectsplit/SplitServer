using System.Text.Json.Serialization;
using SplitServer.Services.RiskEngine.Models;

namespace SplitServer.Responses;

public class GetMostRecentEngineSetupResponse
{
    [JsonPropertyName("economy")]
    public required string Economy { get; init; }

    [JsonPropertyName("financials")]
    public required Financials Financials { get; init; }

    [JsonPropertyName("risk_toggles")]
    public required RiskToggles RiskToggles { get; init; }

    [JsonPropertyName("custom_risks")]
    public required List<CustomRisk> CustomRisks { get; init; }

    [JsonPropertyName("correlations")]
    public CorrelationInput? Correlations { get; init; }
}
