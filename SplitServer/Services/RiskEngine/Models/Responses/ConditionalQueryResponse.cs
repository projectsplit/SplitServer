using System.Text.Json.Serialization;

public class ConditionalQueryResponse
{
    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = string.Empty;

    [JsonPropertyName("condition")]
    public string Condition { get; set; } = string.Empty;

    [JsonPropertyName("n_total")]
    public int NTotal { get; set; }

    [JsonPropertyName("n_subset")]
    public int NSubset { get; set; }

    [JsonPropertyName("frac_subset")]
    public double FracSubset { get; set; }

    [JsonPropertyName("n_busts_total")]
    public int NBustsTotal { get; set; }

    [JsonPropertyName("n_busts_in_subset")]
    public int NBustsInSubset { get; set; }

    [JsonPropertyName("frac_busts")]
    public double FracBusts { get; set; }

    [JsonPropertyName("p_bust")]
    public double? PBust { get; set; }

    [JsonPropertyName("baseline_p_bust")]
    public double BaselinePBust { get; set; }

    [JsonPropertyName("lift")]
    public double? Lift { get; set; }

    [JsonPropertyName("narrative")]
    public List<string> Narrative { get; set; } = new();

    [JsonPropertyName("factor_explanations")]
    public Dictionary<string, List<string>> FactorExplanations { get; set; } = new();
}
