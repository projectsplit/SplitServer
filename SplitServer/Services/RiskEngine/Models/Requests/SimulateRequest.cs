using System.Text.Json.Serialization;
using SplitServer.Services.RiskEngine.Models;

public class SimulationRequest
{
    [JsonPropertyName("economy")]
    public string Economy { get; set; } = "UK";

    [JsonPropertyName("financials")]
    public Financials Financials { get; set; } = null!;

    [JsonPropertyName("risk_toggles")]
    public RiskToggles RiskToggles { get; set; } = null!;

    [JsonPropertyName("custom_risks")]
    public List<CustomRisk> CustomRisks { get; set; } = new();

    [JsonPropertyName("correlations")]
    public CorrelationInput? Correlations { get; set; }
}