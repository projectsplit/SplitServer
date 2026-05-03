using System.Text.Json.Serialization;

namespace SplitServer.Services.RiskEngine.Models;

public class CustomRisk
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("once_every_x_years")]
    public double OnceEveryXYears { get; set; }

    [JsonPropertyName("opt_loss")]
    public double OptLoss { get; set; }

    [JsonPropertyName("pess_loss")]
    public double PessLoss { get; set; }

    [JsonPropertyName("sev_dist")]
    public string SevDist { get; set; } = "L";

    [JsonPropertyName("freq_dist")]
    public string FreqDist { get; set; } = "P";

    [JsonPropertyName("recoverable")]
    public double Recoverable { get; set; } = 0.0;

    [JsonPropertyName("attributable")]
    public double Attributable { get; set; } = 0.0;
}