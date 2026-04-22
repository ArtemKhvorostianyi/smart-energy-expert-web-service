namespace SmartEnergyExpert.Api.DTOs;

public sealed class EvaluationResultResponse
{
    public Guid EvaluationId { get; init; }
    public decimal IntegralScore { get; init; }
    public string RiskLevel { get; init; } = string.Empty;
    public string Recommendation { get; init; } = string.Empty;
}
