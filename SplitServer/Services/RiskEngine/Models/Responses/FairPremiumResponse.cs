using System.Text.Json.Serialization;

public class FairPremiumResponse
{
    [JsonPropertyName("run_id")]    public string RunId { get; set; } = string.Empty;
    [JsonPropertyName("risk_name")] public string RiskName { get; set; } = string.Empty;
    [JsonPropertyName("max_loss")]  public double? MaxLoss { get; set; }
    [JsonPropertyName("premium")]   public double Premium { get; set; }
    [JsonPropertyName("basis")]     public string Basis { get; set; } = "loss";
}
