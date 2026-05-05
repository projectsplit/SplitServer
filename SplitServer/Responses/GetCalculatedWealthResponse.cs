using System.Text.Json.Serialization;

namespace SplitServer.Responses;

public class GetCalculatedWealthResponse
{
    [JsonPropertyName("run_id")]
    public required string RunId { get; init; }

    [JsonPropertyName("starting_wealth")]
    public required double StartingWealth { get; init; }

    [JsonPropertyName("economy")]
    public required EconomyInfo Economy { get; init; }

    [JsonPropertyName("summary")]
    public required SimulationSummary Summary { get; init; }

    [JsonPropertyName("scenarios")]
    public required List<ScenarioRow> Scenarios { get; init; }

    [JsonPropertyName("n_sims")]
    public required int NSims { get; init; }

    [JsonPropertyName("realized_correlation")]
    public double? RealizedCorrelation { get; init; }
}
