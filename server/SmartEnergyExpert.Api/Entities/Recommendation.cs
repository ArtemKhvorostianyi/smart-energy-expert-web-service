namespace SmartEnergyExpert.Api.Entities;

public sealed class Recommendation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ComparisonRunId { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string SuggestedAction { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ComparisonRun? ComparisonRun { get; set; }
}
