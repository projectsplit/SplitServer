using System.Text.Json.Serialization;

public sealed class TailDriversRequest
{
    [JsonPropertyName("exclude_property")]
    public bool ExcludeProperty { get; set; } = false;

    [JsonPropertyName("tail_threshold_busts")]
    public int TailThresholdBusts { get; set; } = 50;

    [JsonPropertyName("tail_fallback_pct")]
    public double TailFallbackPct { get; set; } = 0.5;

    [JsonPropertyName("pair_quantile")]
    public double PairQuantile { get; set; } = 0.25;

    [JsonPropertyName("pair_top_n")]
    public int PairTopN { get; set; } = 10;

    [JsonPropertyName("path_depth")]
    public int PathDepth { get; set; } = 3;

    [JsonPropertyName("path_top_n")]
    public int PathTopN { get; set; } = 5;
}
