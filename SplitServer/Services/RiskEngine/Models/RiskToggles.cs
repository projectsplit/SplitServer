using System.Text.Json.Serialization;

namespace SplitServer.Services.RiskEngine.Models;

public class RiskToggles
{
    [JsonPropertyName("equities")]
    public bool Equities { get; set; } = true;

    [JsonPropertyName("yields")]
    public bool Yields { get; set; } = true;

    [JsonPropertyName("inflation")]
    public bool Inflation { get; set; } = true;

    [JsonPropertyName("property")]
    public bool Property { get; set; } = true;

    [JsonPropertyName("career_loss")]
    public bool CareerLoss { get; set; } = true;

    [JsonPropertyName("career_opt_loss")]
    public double CareerOptLoss { get; set; } = 9600;

    [JsonPropertyName("career_pess_loss")]
    public double CareerPessLoss { get; set; } = 32000;

    [JsonPropertyName("career_recoverable")]
    public double CareerRecoverable { get; set; } = 0.50;

    [JsonPropertyName("severance_invest_rate")]
    public double SeveranceInvestRate { get; set; } = 0.0;
}
