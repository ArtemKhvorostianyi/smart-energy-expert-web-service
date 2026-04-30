namespace SmartEnergyExpert.Api.Entities;

public sealed class ComparisonRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SimulationDatasetId { get; set; }
    public Guid FieldDatasetId { get; set; }
    public string Status { get; set; } = "completed";
    public decimal Mae { get; set; }
    public decimal Rmse { get; set; }
    public decimal MeanRelativeErrorPercent { get; set; }
    public decimal P95AbsoluteError { get; set; }
    public int TotalComparedPoints { get; set; }
    public int SignificantDifferenceCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Dataset? SimulationDataset { get; set; }
    public Dataset? FieldDataset { get; set; }
    public ICollection<DifferencePoint> Differences { get; set; } = new List<DifferencePoint>();
    public ICollection<Recommendation> Recommendations { get; set; } = new List<Recommendation>();
}
