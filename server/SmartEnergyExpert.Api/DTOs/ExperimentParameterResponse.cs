namespace SmartEnergyExpert.Api.DTOs;

public sealed class ExperimentParameterResponse
{
    public Guid Id { get; init; }
    public Guid ExperimentId { get; init; }
    public string ParameterName { get; init; } = string.Empty;
    public decimal Value { get; init; }
    public string Unit { get; init; } = string.Empty;
    public decimal? MinAcceptable { get; init; }
    public decimal? MaxAcceptable { get; init; }
    public decimal? Weight { get; init; }
    public string Category { get; init; } = "physical";
    public string? Description { get; init; }
    public bool IsCritical { get; init; }
    public string Source { get; init; } = "manual";
    public DateTimeOffset? MeasuredAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
