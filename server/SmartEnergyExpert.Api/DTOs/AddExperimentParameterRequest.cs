namespace SmartEnergyExpert.Api.DTOs;

public sealed class AddExperimentParameterRequest
{
    public string ParameterName { get; init; } = string.Empty;
    public decimal Value { get; init; }
    public string Unit { get; init; } = string.Empty;
    public DateTimeOffset? MeasuredAt { get; init; }
}
