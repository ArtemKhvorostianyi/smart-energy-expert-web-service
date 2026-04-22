namespace SmartEnergyExpert.Api.Entities;

public sealed class Evaluation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExperimentId { get; set; }
    public Guid ExpertId { get; set; }
    public decimal IntegralScore { get; set; }
    public string RiskLevel { get; set; } = "low";
    public string? Conclusion { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Experiment? Experiment { get; set; }
    public User? Expert { get; set; }
    public Recommendation? Recommendation { get; set; }
}
