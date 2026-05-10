using System.Text.Json.Serialization;

public sealed class TailDriversResponse
{
    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = "";

    [JsonPropertyName("tail_label")]
    public string TailLabel { get; set; } = "";

    [JsonPropertyName("baseline_p_bust")]
    public double BaselinePBust { get; set; }

    [JsonPropertyName("archetype")]
    public string Archetype { get; set; } = "";

    [JsonPropertyName("factors")]
    public Dictionary<string, string> Factors { get; set; } = new();

    [JsonPropertyName("zscores")]
    public List<ZScoreRow> ZScores { get; set; } = new();

    [JsonPropertyName("pairs")]
    public List<PairRow> Pairs { get; set; } = new();

    [JsonPropertyName("pathways")]
    public PathwaysResult Pathways { get; set; } = new();

    [JsonPropertyName("narrative")]
    public NarrativeSections Narrative { get; set; } = new();
}

public sealed class ZScoreRow
{
    [JsonPropertyName("factor")]
    public string Factor { get; set; } = "";

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "";

    [JsonPropertyName("mean_full")]
    public double MeanFull { get; set; }

    [JsonPropertyName("std_full")]
    public double StdFull { get; set; }

    [JsonPropertyName("mean_tail")]
    public double MeanTail { get; set; }

    [JsonPropertyName("z")]
    public double Z { get; set; }

    [JsonPropertyName("abs_z")]
    public double AbsZ { get; set; }
}

public sealed class PairRow
{
    [JsonPropertyName("factor_a")]
    public string FactorA { get; set; } = "";

    [JsonPropertyName("direction_a")]
    public string DirectionA { get; set; } = "";

    [JsonPropertyName("thresh_a")]
    public double ThreshA { get; set; }

    [JsonPropertyName("factor_b")]
    public string FactorB { get; set; } = "";

    [JsonPropertyName("direction_b")]
    public string DirectionB { get; set; } = "";

    [JsonPropertyName("thresh_b")]
    public double ThreshB { get; set; }

    [JsonPropertyName("q")]
    public double Q { get; set; }

    [JsonPropertyName("p_baseline")]
    public double PBaseline { get; set; }

    [JsonPropertyName("p_a_alone")]
    public double PAlone { get; set; }

    [JsonPropertyName("p_b_alone")]
    public double PBAlone { get; set; }

    [JsonPropertyName("p_joint")]
    public double PJoint { get; set; }

    [JsonPropertyName("expected_indep")]
    public double ExpectedIndep { get; set; }

    [JsonPropertyName("interaction_excess")]
    public double InteractionExcess { get; set; }

    [JsonPropertyName("joint_excess")]
    public double JointExcess { get; set; }

    [JsonPropertyName("cells")]
    public Dictionary<string, PairCell> Cells { get; set; } = new();
}

public sealed class PairCell
{
    [JsonPropertyName("n")]
    public int N { get; set; }

    [JsonPropertyName("p_bust")]
    public double PBust { get; set; }
}

public sealed class PathwaysResult
{
    [JsonPropertyName("available")]
    public bool Available { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("depth")]
    public int? Depth { get; set; }

    [JsonPropertyName("min_samples_leaf")]
    public int? MinSamplesLeaf { get; set; }

    [JsonPropertyName("n_busts")]
    public int? NBusts { get; set; }

    [JsonPropertyName("p_baseline")]
    public double? PBaseline { get; set; }

    [JsonPropertyName("n_leaves")]
    public int? NLeaves { get; set; }

    [JsonPropertyName("leaves")]
    public List<PathwayLeaf> Leaves { get; set; } = new();
}

public sealed class PathwayLeaf
{
    [JsonPropertyName("rules")]
    public List<object[]> Rules { get; set; } = new();

    [JsonPropertyName("n_paths")]
    public int NPaths { get; set; }

    [JsonPropertyName("n_busts")]
    public int NBusts { get; set; }

    [JsonPropertyName("bust_rate")]
    public double BustRate { get; set; }

    [JsonPropertyName("lift_vs_baseline")]
    public double LiftVsBaseline { get; set; }
}

public sealed class NarrativeSections
{
    [JsonPropertyName("headline")]
    public string Headline { get; set; } = "";

    [JsonPropertyName("portrait")]
    public List<string> Portrait { get; set; } = new();

    [JsonPropertyName("diagnosis")]
    public List<string> Diagnosis { get; set; } = new();

    [JsonPropertyName("explanation")]
    public string? Explanation { get; set; }    

    [JsonPropertyName("pathways")]
    public List<string> Pathways { get; set; } = new();
}
