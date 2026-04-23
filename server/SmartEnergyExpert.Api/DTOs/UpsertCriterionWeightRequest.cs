namespace SmartEnergyExpert.Api.DTOs;

public sealed class UpsertCriterionWeightRequest
{
    public string ExperimentType { get; init; } = "default";
    public decimal Weight { get; init; } = 1m;
    public bool IsActive { get; init; } = true;
}
