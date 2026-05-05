using System.Text.Json.Serialization;

public class WhatIfResponse
{
    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = string.Empty;

    [JsonPropertyName("baseline")]
    public WhatIfSummary Baseline { get; set; } = new();

    [JsonPropertyName("scenario")]
    public WhatIfSummary Scenario { get; set; } = new();

    [JsonPropertyName("delta")]
    public WhatIfDelta Delta { get; set; } = new();

    [JsonPropertyName("narrative")]
    public WhatIfNarrative Narrative { get; set; } = new();
}

public class WhatIfSummary
{
    [JsonPropertyName("p_bust")]
    public double PBust { get; set; }

    [JsonPropertyName("bust_count")]
    public int BustCount { get; set; }

    [JsonPropertyName("mean")]
    public double Mean { get; set; }

    [JsonPropertyName("median")]
    public double Median { get; set; }

    [JsonPropertyName("p5")]
    public double P5 { get; set; }

    [JsonPropertyName("p25")]
    public double P25 { get; set; }

    [JsonPropertyName("p75")]
    public double P75 { get; set; }

    [JsonPropertyName("p95")]
    public double P95 { get; set; }

    [JsonPropertyName("n_sims")]
    public int NSims { get; set; }
}

public class WhatIfDelta
{
    [JsonPropertyName("delta_p_bust")]
    public double DeltaPBust { get; set; }

    [JsonPropertyName("delta_mean")]
    public double DeltaMean { get; set; }

    [JsonPropertyName("delta_median")]
    public double DeltaMedian { get; set; }

    [JsonPropertyName("delta_p5")]
    public double DeltaP5 { get; set; }

    [JsonPropertyName("delta_p95")]
    public double DeltaP95 { get; set; }
}

public class WhatIfNarrative
{
    [JsonPropertyName("headline")]
    public string Headline { get; set; } = string.Empty;

    [JsonPropertyName("scenario")]
    public List<string> Scenario { get; set; } = new();

    [JsonPropertyName("impact")]
    public List<string> Impact { get; set; } = new();

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;
}
