using System.Text.Json.Serialization;
using SplitServer.Services.RiskEngine.Models;
public class SimulationResponse
{
    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = string.Empty;
    
    [JsonPropertyName("starting_wealth")]
    public double StartingWealth { get; set; }

    [JsonPropertyName("economy")]
    public EconomyInfo Economy { get; set; } = null!;

    [JsonPropertyName("summary")]
    public SimulationSummary Summary { get; set; } = null!;

    [JsonPropertyName("scenarios")]
    public List<ScenarioRow> Scenarios { get; set; } = new();

    [JsonPropertyName("n_sims")]
    public int NSims { get; set; }

    [JsonPropertyName("realized_correlation")]
    public double? RealizedCorrelation { get; set; }
}