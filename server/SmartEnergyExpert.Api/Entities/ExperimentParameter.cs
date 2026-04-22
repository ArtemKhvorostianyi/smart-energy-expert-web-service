namespace SmartEnergyExpert.Api.Entities;

public sealed class ExperimentParameter
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExperimentId { get; set; }
    public string ParameterName { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTimeOffset? MeasuredAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Experiment? Experiment { get; set; }
}
