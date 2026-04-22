namespace SmartEnergyExpert.Api.DTOs;

public sealed class CreateExperimentRequest
{
    public string Title { get; init; } = string.Empty;
    public string ExperimentType { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid CreatedBy { get; init; }
}
