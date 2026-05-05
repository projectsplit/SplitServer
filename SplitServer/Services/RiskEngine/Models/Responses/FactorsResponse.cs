using System.Text.Json.Serialization;

public class FactorsResponse
{
    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = string.Empty;

    [JsonPropertyName("risks")]
    public Dictionary<string, RiskStats> Risks { get; set; } = new();

    [JsonPropertyName("factors")]
    public Dictionary<string, FactorStats> Factors { get; set; } = new();
}

public class FactorStats
{
    [JsonPropertyName("min")]  public double Min  { get; set; }
    [JsonPropertyName("p01")]  public double P01  { get; set; }
    [JsonPropertyName("p50")]  public double P50  { get; set; }
    [JsonPropertyName("p99")]  public double P99  { get; set; }
    [JsonPropertyName("max")]  public double Max  { get; set; }
    [JsonPropertyName("mean")] public double Mean { get; set; }
    [JsonPropertyName("std")]  public double Std  { get; set; }
}

public class RiskStats : FactorStats
{
    [JsonPropertyName("fair_premium")]
    public FairPremium FairPremium { get; set; } = new();
}

public class FairPremium
{
    [JsonPropertyName("full")]
    public double Full { get; set; }

    [JsonPropertyName("caps")]
    public List<FairPremiumCap> Caps { get; set; } = new();

    [JsonPropertyName("basis")]
    public string Basis { get; set; } = "loss";
}

public class FairPremiumCap
{
    [JsonPropertyName("max_loss")]
    public double MaxLoss { get; set; }

    [JsonPropertyName("premium")]
    public double Premium { get; set; }
}
