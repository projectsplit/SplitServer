using System.Text.Json.Serialization;

public class ConditionalQueryRequest
{
    [JsonPropertyName("conditions")]
    public List<Condition> Conditions { get; set; } = new();
}

public class Condition
{
    [JsonPropertyName("factor")]
    public string Factor { get; set; } = string.Empty;

    [JsonPropertyName("op")]
    public string Op { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public double Value { get; set; }
}
