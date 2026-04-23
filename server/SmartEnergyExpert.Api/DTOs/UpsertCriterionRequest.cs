namespace SmartEnergyExpert.Api.DTOs;

public sealed class UpsertCriterionRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal MinValue { get; init; }
    public decimal MaxValue { get; init; }
    public decimal DefaultWeight { get; init; } = 1m;
    public bool IsActive { get; init; } = true;
}
