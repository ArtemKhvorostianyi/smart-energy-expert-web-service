using SmartEnergyExpert.Api.Entities;

namespace SmartEnergyExpert.Api.Services;

public sealed record EvaluationComputationResult(
    decimal Score,
    string RiskLevel,
    string Recommendation,
    string Explanation,
    IReadOnlyList<string> TopFactors,
    string Status);

public interface IEvaluationService
{
    EvaluationComputationResult Calculate(
        Experiment experiment,
        IReadOnlyCollection<Criterion> activeCriteria,
        IReadOnlyDictionary<Guid, decimal> criterionWeights);
}
