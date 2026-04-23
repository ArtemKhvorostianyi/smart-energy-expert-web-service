using SmartEnergyExpert.Api.Entities;

namespace SmartEnergyExpert.Api.Services;

public sealed class EvaluationService : IEvaluationService
{
    public (decimal score, string riskLevel, string recommendation) Calculate(
        Experiment experiment,
        IReadOnlyCollection<Criterion> activeCriteria,
        IReadOnlyDictionary<Guid, decimal> criterionWeights)
    {
        if (experiment.Parameters.Count == 0)
        {
            return (0, "low", "Insufficient data. Add experiment parameters.");
        }

        var criteriaByName = activeCriteria.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        decimal weightedScore = 0m;
        decimal totalWeight = 0m;

        foreach (var parameter in experiment.Parameters)
        {
            if (!criteriaByName.TryGetValue(parameter.ParameterName, out var criterion))
            {
                continue;
            }

            var range = criterion.MaxValue - criterion.MinValue;
            if (range <= 0)
            {
                continue;
            }

            var normalized = (parameter.Value - criterion.MinValue) / range;
            normalized = decimal.Clamp(normalized, 0m, 1m);

            var weight = criterionWeights.TryGetValue(criterion.Id, out var configuredWeight)
                ? configuredWeight
                : criterion.DefaultWeight;

            if (weight <= 0)
            {
                continue;
            }

            weightedScore += normalized * weight;
            totalWeight += weight;
        }

        var score = totalWeight <= 0 ? 0 : weightedScore / totalWeight;

        var (riskLevel, recommendation) = score switch
        {
            <= 0.25m => ("low", "Continue operation with standard monitoring."),
            <= 0.50m => ("moderate", "Schedule additional inspection."),
            <= 0.75m => ("high", "Perform detailed diagnostic as soon as possible."),
            _ => ("critical", "Stop equipment and start incident response procedure.")
        };

        return (score, riskLevel, recommendation);
    }
}
