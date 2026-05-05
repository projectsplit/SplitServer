using System.Text.Json.Serialization;

public class WhatIfRequest
{
    [JsonPropertyName("buffer_delta")]
    public double BufferDelta { get; set; }

    [JsonPropertyName("expense_cut")]
    public double ExpenseCut { get; set; }

    [JsonPropertyName("salary_delta")]
    public double SalaryDelta { get; set; }

    [JsonPropertyName("reweight")]
    public ReweightRequest? Reweight { get; set; }

    [JsonPropertyName("disabled_risks")]
    public Dictionary<string, double> DisabledRisks { get; set; } = new();

    [JsonPropertyName("risk_caps")]
    public Dictionary<string, double[]> RiskCaps { get; set; } = new();

    [JsonPropertyName("exclude_property")]
    public bool ExcludeProperty { get; set; }
}

public class ReweightRequest
{
    [JsonPropertyName("equity")]
    public double Equity { get; set; }

    [JsonPropertyName("bond")]
    public double Bond { get; set; }

    [JsonPropertyName("total")]
    public double? Total { get; set; }
}
