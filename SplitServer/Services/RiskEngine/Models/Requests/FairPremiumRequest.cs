using System.Text.Json.Serialization;

public class FairPremiumRequest
{
    [JsonPropertyName("risk_name")] public string RiskName { get; set; } = string.Empty;
    [JsonPropertyName("max_loss")]  public double? MaxLoss { get; set; }
}
