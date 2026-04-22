using SmartEnergyExpert.Api.Entities;

namespace SmartEnergyExpert.Api.Services;

public sealed class EvaluationService : IEvaluationService
{
    public (decimal score, string riskLevel, string recommendation) Calculate(Experiment experiment)
    {
        if (experiment.Parameters.Count == 0)
        {
            return (0, "low", "Insufficient data. Add experiment parameters.");
        }

        var avg = experiment.Parameters.Average(p => p.Value);
        var normalized = Math.Clamp((double)(avg / 100m), 0.0, 1.0);
        var score = (decimal)normalized;

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
