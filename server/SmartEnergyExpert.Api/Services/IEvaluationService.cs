using SmartEnergyExpert.Api.Entities;

namespace SmartEnergyExpert.Api.Services;

public interface IEvaluationService
{
    (decimal score, string riskLevel, string recommendation) Calculate(
        Experiment experiment,
        IReadOnlyCollection<Criterion> activeCriteria,
        IReadOnlyDictionary<Guid, decimal> criterionWeights);
}
