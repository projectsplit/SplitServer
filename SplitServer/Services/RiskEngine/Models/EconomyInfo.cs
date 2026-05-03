using System.Text.Json.Serialization;

public class EconomyInfo
{
    [JsonPropertyName("requested")]
    public string Requested { get; set; } = string.Empty;

    [JsonPropertyName("resolved_yields")]
    public string ResolvedYields { get; set; } = string.Empty;

    [JsonPropertyName("resolved_inflation")]
    public string ResolvedInflation { get; set; } = string.Empty;

    [JsonPropertyName("resolved_property")]
    public string ResolvedProperty { get; set; } = string.Empty;
}