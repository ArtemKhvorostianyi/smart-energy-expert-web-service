using Microsoft.EntityFrameworkCore;
using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.Entities;

namespace SmartEnergyExpert.Api.Services;

public sealed class ComparisonService(AppDbContext dbContext) : IComparisonService
{
    public async Task<ComparisonComputationResult> CompareAsync(
        Dataset simulationDataset,
        Dataset fieldDataset,
        int topN,
        CancellationToken cancellationToken)
    {
        var simulationSamples = await dbContext.AcousticSamples
            .AsNoTracking()
            .Where(x => x.DatasetId == simulationDataset.Id)
            .ToListAsync(cancellationToken);
        var fieldSamples = await dbContext.AcousticSamples
            .AsNoTracking()
            .Where(x => x.DatasetId == fieldDataset.Id)
            .ToListAsync(cancellationToken);

        var fieldLookup = fieldSamples.ToDictionary(
            x => (x.Timestamp.UtcDateTime, x.FrequencyBand),
            x => x,
            EqualityComparer<(DateTime, decimal)>.Default);

        var differences = new List<DifferencePoint>();
        foreach (var simulation in simulationSamples)
        {
            if (!fieldLookup.TryGetValue((simulation.Timestamp.UtcDateTime, simulation.FrequencyBand), out var field))
            {
                continue;
            }

            var absError = Math.Abs(simulation.AmplitudeDb - field.AmplitudeDb);
            var relErrorPercent = field.AmplitudeDb == 0
                ? 0
                : Math.Abs((simulation.AmplitudeDb - field.AmplitudeDb) / field.AmplitudeDb) * 100;
            var severity = relErrorPercent switch
            {
                >= 40 => "critical",
                >= 25 => "high",
                >= 10 => "moderate",
                _ => "low"
            };

            differences.Add(new DifferencePoint
            {
                Timestamp = simulation.Timestamp,
                FrequencyBand = simulation.FrequencyBand,
                SimulationValue = simulation.AmplitudeDb,
                FieldValue = field.AmplitudeDb,
                AbsoluteError = decimal.Round(absError, 4),
                RelativeErrorPercent = decimal.Round(relErrorPercent, 4),
                Severity = severity,
                Explanation = BuildDifferenceExplanation(simulation, field, relErrorPercent)
            });
        }

        var comparedPoints = differences.Count;
        var mae = comparedPoints == 0 ? 0 : differences.Average(x => x.AbsoluteError);
        var rmse = comparedPoints == 0
            ? 0
            : (decimal)Math.Sqrt((double)differences.Average(x => x.AbsoluteError * x.AbsoluteError));
        var mre = comparedPoints == 0 ? 0 : differences.Average(x => x.RelativeErrorPercent);
        var sortedAbsErrors = differences.OrderBy(x => x.AbsoluteError).Select(x => x.AbsoluteError).ToArray();
        var p95 = sortedAbsErrors.Length == 0
            ? 0
            : sortedAbsErrors[(int)Math.Floor((sortedAbsErrors.Length - 1) * 0.95)];
        var significantCount = differences.Count(x => x.Severity is "high" or "critical");

        return new ComparisonComputationResult
        {
            Mae = decimal.Round(mae, 4),
            Rmse = decimal.Round(rmse, 4),
            MeanRelativeErrorPercent = decimal.Round(mre, 4),
            P95AbsoluteError = decimal.Round(p95, 4),
            TotalComparedPoints = comparedPoints,
            SignificantDifferenceCount = significantCount,
            TopDifferences = differences
                .OrderByDescending(x => x.RelativeErrorPercent)
                .ThenByDescending(x => x.AbsoluteError)
                .Take(Math.Max(1, topN))
                .ToArray(),
            Recommendations = BuildRecommendations(mae, mre, significantCount, comparedPoints)
        };
    }

    private static IReadOnlyList<Recommendation> BuildRecommendations(
        decimal mae,
        decimal mre,
        int significantCount,
        int totalComparedPoints)
    {
        var list = new List<Recommendation>();
        var significantShare = totalComparedPoints == 0 ? 0 : (decimal)significantCount / totalComparedPoints;

        if (mre >= 30)
        {
            list.Add(new Recommendation
            {
                ReasonCode = "FREQ_MODEL_MISMATCH",
                Explanation = "Large relative error indicates likely frequency-response mismatch between model and field channel.",
                SuggestedAction = "Recalibrate absorption/spreading parameters and rerun on matched frequency bands.",
                Confidence = 0.86m
            });
        }

        if (mae >= 6)
        {
            list.Add(new Recommendation
            {
                ReasonCode = "CALIBRATION_DRIFT",
                Explanation = "Absolute amplitude gap suggests hydrophone/transmitter calibration drift.",
                SuggestedAction = "Validate gain chain with reference source and update calibration coefficients.",
                Confidence = 0.78m
            });
        }

        if (significantShare >= 0.35m)
        {
            list.Add(new Recommendation
            {
                ReasonCode = "ENVIRONMENT_VARIANCE",
                Explanation = "High share of significant points often appears when sound speed profile/noise differs from modeled conditions.",
                SuggestedAction = "Use field CTD/noise profile for rerun and align bathymetry assumptions.",
                Confidence = 0.72m
            });
        }

        if (list.Count == 0)
        {
            list.Add(new Recommendation
            {
                ReasonCode = "MODEL_ACCEPTABLE",
                Explanation = "Observed differences are mostly low and fall within expected experimental variance.",
                SuggestedAction = "Keep monitoring and expand validation set before model promotion.",
                Confidence = 0.67m
            });
        }

        return list;
    }

    private static string BuildDifferenceExplanation(AcousticSample simulation, AcousticSample field, decimal relativeErrorPercent)
    {
        if (relativeErrorPercent >= 30)
        {
            return "Potential mismatch in modeled propagation losses or local environmental parameters.";
        }

        if (Math.Abs(simulation.DepthMeters - field.DepthMeters) > 5)
        {
            return "Depth mismatch can alter multipath structure and explains moderate deviation.";
        }

        if (Math.Abs(simulation.RangeMeters - field.RangeMeters) > 50)
        {
            return "Range offset likely contributes to amplitude discrepancy.";
        }

        return "Difference is within routine field variance.";
    }
}
