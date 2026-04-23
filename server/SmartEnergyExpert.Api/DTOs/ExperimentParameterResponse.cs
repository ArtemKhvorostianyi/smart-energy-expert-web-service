namespace SmartEnergyExpert.Api.DTOs;

public sealed class ExperimentParameterResponse
{
    public Guid Id { get; init; }
    public Guid ExperimentId { get; init; }
    public string ParameterName { get; init; } = string.Empty;
    public decimal Value { get; init; }
    public string Unit { get; init; } = string.Empty;
    public DateTimeOffset? MeasuredAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
