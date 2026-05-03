using System.Text.Json.Serialization;

namespace SplitServer.Services.RiskEngine.Models;

public class CorrelationInput
{
    [JsonPropertyName("pairs")]
    public Dictionary<string, Dictionary<string, double>> Pairs { get; set; } = new();
}