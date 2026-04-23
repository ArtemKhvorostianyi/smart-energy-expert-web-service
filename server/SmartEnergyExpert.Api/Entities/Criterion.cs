namespace SmartEnergyExpert.Api.Entities;

public sealed class Criterion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MinValue { get; set; }
    public decimal MaxValue { get; set; }
    public decimal DefaultWeight { get; set; } = 1m;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<CriterionWeight> CriterionWeights { get; set; } = new List<CriterionWeight>();
}
