namespace SmartEnergyExpert.Api.Entities;

public sealed class Dataset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "simulation";
    public string SourceSystem { get; set; } = "synthetic";
    public string Version { get; set; } = "v1";
    public DateTimeOffset TimeRangeStart { get; set; }
    public DateTimeOffset TimeRangeEnd { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<AcousticSample> Samples { get; set; } = new List<AcousticSample>();
}
