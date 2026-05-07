using System.Text.Json;
using System.Text.Json.Serialization;

public class ScenarioRow
{
    [JsonPropertyName("percentile")]
    public double Percentile { get; set; }

    [JsonPropertyName("wealth")]
    public double Wealth { get; set; }

    [JsonPropertyName("equity_return")]
    public double EquityReturn { get; set; }

    [JsonPropertyName("portfolio_end")]
    public double PortfolioEnd { get; set; }

    [JsonPropertyName("bond_portfolio_end")]
    public double BondPortfolioEnd { get; set; }

    [JsonPropertyName("career_severance")]
    public double CareerSeverance { get; set; }

    [JsonPropertyName("salary_cash")]
    public decimal? SalaryCash { get; init; } 

    [JsonPropertyName("severance_cash")]
    public decimal? SeveranceCash { get; init; }

    [JsonPropertyName("income")]
    public double Income { get; set; }

    [JsonPropertyName("expenses")]
    public double Expenses { get; set; }

    [JsonPropertyName("bond_pnl")]
    public double? BondPnl { get; set; }

    [JsonPropertyName("delta_y_bps")]
    public double? DeltaYBps { get; set; }

    [JsonPropertyName("delta_infl_1yr")]
    public double? DeltaInfl1yr { get; set; }

    [JsonPropertyName("property_return")]
    public double? PropertyReturn { get; set; }

    [JsonPropertyName("property_end")]
    public double? PropertyEnd { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? AdditionalRisks { get; set; }
}
