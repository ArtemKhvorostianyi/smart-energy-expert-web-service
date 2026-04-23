namespace SmartEnergyExpert.Api.Entities;

public sealed class CriterionWeight
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CriterionId { get; set; }
    public string ExperimentType { get; set; } = string.Empty;
    public decimal Weight { get; set; } = 1m;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Criterion? Criterion { get; set; }
}
