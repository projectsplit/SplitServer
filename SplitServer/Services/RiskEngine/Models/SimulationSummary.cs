using System.Text.Json.Serialization;

public class SimulationSummary
{
    [JsonPropertyName("mean")]
    public double Mean { get; set; }

    [JsonPropertyName("median")]
    public double Median { get; set; }

    [JsonPropertyName("std")]
    public double Std { get; set; }

    [JsonPropertyName("min")]
    public double Min { get; set; }

    [JsonPropertyName("max")]
    public double Max { get; set; }

    [JsonPropertyName("prob_negative")]
    public double ProbNegative { get; set; }

    [JsonPropertyName("percentiles")]
    public Dictionary<string, double> Percentiles { get; set; } = new();
}