using System.Text.Json.Serialization;

namespace SplitServer.Services.OpenExchangeRates.Models;

public class OpenExchangeRatesHistoricalResponse
{
    [JsonPropertyName("disclaimer")]
    public required string Disclaimer { get; init; }

    [JsonPropertyName("license")]
    public required string License { get; init; }

    [JsonPropertyName("timestamp")]
    public required long Timestamp { get; init; }

    [JsonPropertyName("base")]
    public required string Base { get; init; }

    [JsonPropertyName("rates")]
    public required Dictionary<string, decimal> Rates { get; init; }
}