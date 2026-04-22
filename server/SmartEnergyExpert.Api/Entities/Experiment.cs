namespace SmartEnergyExpert.Api.Entities;

public sealed class Experiment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string ExperimentType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "draft";
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User? Creator { get; set; }
    public ICollection<ExperimentParameter> Parameters { get; set; } = new List<ExperimentParameter>();
}
