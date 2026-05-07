using System.Text.Json.Serialization;

public record ConditionalSweepResponse
{
    [JsonPropertyName("run_id")]
    public required string RunId { get; init; }

    [JsonPropertyName("factor")]
    public required string Factor { get; init; }

    [JsonPropertyName("op")]
    public required string Op { get; init; }

    [JsonPropertyName("thresholds")]
    public required List<double> Thresholds { get; init; }

    [JsonPropertyName("rows")]
    public required List<SweepRow> Rows { get; init; }

    [JsonPropertyName("narrative")]
    public required SweepNarrative Narrative { get; init; }

    [JsonPropertyName("factor_explanations")]
    public required Dictionary<string, List<string>> FactorExplanations { get; init; }
}

public record SweepRow
{
    [JsonPropertyName("condition")]
    public required string Condition { get; init; }

    [JsonPropertyName("n_total")]
    public required int NTotal { get; init; }

    [JsonPropertyName("n_subset")]
    public required int NSubset { get; init; }

    [JsonPropertyName("frac_subset")]
    public required double FracSubset { get; init; }

    [JsonPropertyName("n_busts_total")]
    public required int NBustsTotal { get; init; }

    [JsonPropertyName("n_busts_in_subset")]
    public required int NBustsInSubset { get; init; }

    [JsonPropertyName("frac_busts")]
    public required double FracBusts { get; init; }

    [JsonPropertyName("p_bust")]
    public double? PBust { get; init; }

    [JsonPropertyName("p_bust_ci_low")]
    public double? PBustCiLow { get; init; }

    [JsonPropertyName("p_bust_ci_high")]
    public double? PBustCiHigh { get; init; }

    [JsonPropertyName("baseline_p_bust")]
    public required double BaselinePBust { get; init; }

    [JsonPropertyName("lift")]
    public double? Lift { get; init; }

    [JsonPropertyName("reliability")]
    public required string Reliability { get; init; }
}

public record SweepNarrative
{
    [JsonPropertyName("header")]
    public required List<string> Header { get; init; }

    [JsonPropertyName("body")]
    public required List<string> Body { get; init; }

    [JsonPropertyName("conclusion")]
    public required List<string> Conclusion { get; init; }
}
