using System.Text.Json.Serialization;

namespace SplitServer.Services.RiskEngine.Models;

public class Financials
{
    [JsonPropertyName("savings")]
    public double Savings { get; set; }

    [JsonPropertyName("net_salary")]
    public double NetSalary { get; set; }

    [JsonPropertyName("savings_rate")]
    public double SavingsRate { get; set; }

    [JsonPropertyName("equity_value")]
    public double EquityValue { get; set; }

    [JsonPropertyName("bond_value")]
    public double BondValue { get; set; }

    [JsonPropertyName("property_value")]
    public double PropertyValue { get; set; }

    [JsonPropertyName("bond_tenor")]
    public int BondTenor { get; set; } = 10;
}