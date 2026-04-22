namespace SmartEnergyExpert.Api.DTOs;

public sealed class EvaluateExperimentRequest
{
    public Guid ExpertId { get; init; }
    public string? Conclusion { get; init; }
}
