namespace SmartEnergyExpert.Api.Entities;

public sealed class DifferencePoint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ComparisonRunId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public decimal FrequencyBand { get; set; }
    public decimal SimulationValue { get; set; }
    public decimal FieldValue { get; set; }
    public decimal AbsoluteError { get; set; }
    public decimal RelativeErrorPercent { get; set; }
    public string Severity { get; set; } = "low";
    public string Explanation { get; set; } = string.Empty;

    public ComparisonRun? ComparisonRun { get; set; }
}
