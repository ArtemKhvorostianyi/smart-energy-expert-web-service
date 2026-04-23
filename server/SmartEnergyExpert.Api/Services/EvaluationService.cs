using SmartEnergyExpert.Api.Entities;

namespace SmartEnergyExpert.Api.Services;

public sealed class EvaluationService : IEvaluationService
{
    private sealed record ParameterRiskContribution(string Name, decimal Contribution, bool IsOutOfRange);

    public EvaluationComputationResult Calculate(
        Experiment experiment,
        IReadOnlyCollection<Criterion> activeCriteria,
        IReadOnlyDictionary<Guid, decimal> criterionWeights)
    {
        if (experiment.Parameters.Count == 0)
        {
            return new EvaluationComputationResult(
                0,
                "low",
                "Insufficient data. Add experiment parameters.",
                "No technical parameters were provided for evaluation.",
                [],
                "insufficient_data");
        }

        var criteriaByName = activeCriteria.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var contributions = new List<ParameterRiskContribution>();
        decimal weightedRisk = 0m;
        decimal totalWeight = 0m;

        foreach (var parameter in experiment.Parameters)
        {
            criteriaByName.TryGetValue(parameter.ParameterName, out var criterion);

            var min = parameter.MinAcceptable ?? criterion?.MinValue;
            var max = parameter.MaxAcceptable ?? criterion?.MaxValue;

            if (min is null || max is null || max <= min)
            {
                continue;
            }

            var range = max.Value - min.Value;
            var normalizedDeviation = parameter.Value < min
                ? (min.Value - parameter.Value) / range
                : parameter.Value > max
                    ? (parameter.Value - max.Value) / range
                    : 0m;

            normalizedDeviation = decimal.Clamp(normalizedDeviation, 0m, 1m);
            var isOutOfRange = normalizedDeviation > 0;

            var configuredCriterionWeight = criterion is not null && criterionWeights.TryGetValue(criterion.Id, out var cw)
                ? cw
                : (decimal?)null;
            var baseWeight = parameter.Weight
                ?? configuredCriterionWeight
                ?? criterion?.DefaultWeight
                ?? 1m;

            if (baseWeight <= 0)
            {
                continue;
            }

            var criticalMultiplier = parameter.IsCritical ? 1.5m : 1m;
            var effectiveWeight = baseWeight * criticalMultiplier;
            var contribution = normalizedDeviation * effectiveWeight;

            weightedRisk += contribution;
            totalWeight += effectiveWeight;
            contributions.Add(new ParameterRiskContribution(parameter.ParameterName, contribution, isOutOfRange));
        }

        var score = totalWeight <= 0 ? 0 : decimal.Clamp(weightedRisk / totalWeight, 0m, 1m);

        var (riskLevel, recommendation) = score switch
        {
            <= 0.25m => ("low", "Continue operation with standard monitoring."),
            <= 0.50m => ("moderate", "Schedule additional inspection."),
            <= 0.75m => ("high", "Perform detailed diagnostic as soon as possible."),
            _ => ("critical", "Stop equipment and start incident response procedure.")
        };

        var topFactors = contributions
            .OrderByDescending(x => x.Contribution)
            .Take(3)
            .Where(x => x.Contribution > 0)
            .Select(x => x.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var outOfRangeCount = contributions.Count(x => x.IsOutOfRange);
        var explanation = outOfRangeCount == 0
            ? "All measured parameters are within acceptable ranges."
            : $"Detected {outOfRangeCount} out-of-range parameter(s). Top contributors: {(topFactors.Length == 0 ? "none" : string.Join(", ", topFactors))}.";

        var status = riskLevel switch
        {
            "low" => "normal",
            "moderate" => "requires_attention",
            "high" => "requires_action",
            _ => "critical"
        };

        return new EvaluationComputationResult(score, riskLevel, recommendation, explanation, topFactors, status);
    }
}
