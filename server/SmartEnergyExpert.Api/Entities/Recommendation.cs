namespace SmartEnergyExpert.Api.Entities;

public sealed class Recommendation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ComparisonRunId { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    /// <summary>How inference was formed (deterministic thresholds for this milestone).</summary>
    public string InferenceMethod { get; set; } = string.Empty;
    /// <summary>Short clarification of what the confidence score expresses.</summary>
    public string ConfidenceRationale { get; set; } = string.Empty;
    /// <summary>JSON array of concise evidence bullets (rule outputs / metrics).</summary>
    public string EvidenceSignalsJson { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string SuggestedAction { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ComparisonRun? ComparisonRun { get; set; }
}
