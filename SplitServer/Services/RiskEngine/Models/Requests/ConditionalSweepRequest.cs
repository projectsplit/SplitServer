using System.Text.Json.Serialization;

public record ConditionalSweepRequest
{
    [JsonPropertyName("factor")]
    public required string Factor { get; init; }

    [JsonPropertyName("op")]
    public required string Op { get; init; }

    [JsonPropertyName("thresholds")]
    public List<double>? Thresholds { get; init; }

    [JsonPropertyName("auto_quantiles")]
    public List<double>? AutoQuantiles { get; init; }
}
