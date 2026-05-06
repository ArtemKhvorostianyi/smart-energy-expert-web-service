using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.Entities;

namespace SmartEnergyExpert.Api.Services;

public sealed class ComparisonService(AppDbContext dbContext) : IComparisonService
{
    internal static readonly JsonSerializerOptions RecommendationJson =
        new(JsonSerializerDefaults.Web) { WriteIndented = false };

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
            var refMagnitude = Math.Max(Math.Abs(field.AmplitudeDb), 20m);
            var relErrorPercent = Math.Abs(simulation.AmplitudeDb - field.AmplitudeDb) / refMagnitude * 100;
            var severity = ResolveSeverity(relErrorPercent);

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
        var significantPoints = differences.Where(x =>
            string.Equals(x.Severity, "moderate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.Severity, "high", StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.Severity, "critical", StringComparison.OrdinalIgnoreCase)).ToArray();

        var significantCount = significantPoints.Length;
        var significantShare =
            comparedPoints == 0 ? 0 : (decimal)significantCount / comparedPoints;

        var sortedTop = differences
            .OrderByDescending(x => x.RelativeErrorPercent)
            .ThenByDescending(x => x.AbsoluteError)
            .Take(Math.Max(1, topN));

        var topDifferencesArray = sortedTop.ToArray();

        var insights = BuildRunInsights(significantPoints, differences);
        var dominantBand =
            ResolveDominantBandForVisualization(significantPoints, differences);

        var visualization = BuildVisualizationArtifacts(
            simulationSamples,
            fieldLookup,
            differences,
            dominantBand);

        var recommendations = BuildRecommendations(
            insights,
            mae,
            mre,
            significantCount,
            significantShare,
            comparedPoints).ToArray();

        return new ComparisonComputationResult
        {
            Mae = decimal.Round(mae, 4),
            Rmse = decimal.Round(rmse, 4),
            MeanRelativeErrorPercent = decimal.Round(mre, 4),
            P95AbsoluteError = decimal.Round(p95, 4),
            TotalComparedPoints = comparedPoints,
            SignificantDifferenceCount = significantCount,
            TopDifferences = topDifferencesArray,
            Recommendations = recommendations,
            Visualization = visualization
        };
    }

    private static RunInsights BuildRunInsights(
        IReadOnlyList<DifferencePoint> significantPoints,
        IReadOnlyList<DifferencePoint> allPoints)
    {
        if (significantPoints.Count == 0)
        {
            return new RunInsights(
                ConcentrationDominantBandShareWeighted: 0,
                DominantBand: null,
                TemporalSecondsStdHighRel: 0,
                RelErrorCoefficientOfVariation: 0,
                MultiBandElevatedBuckets: 0);
        }

        var weightTotal = significantPoints.Sum(x => x.RelativeErrorPercent);
        if (weightTotal == 0)
        {
            weightTotal = 1;
        }

        var weightedTopShare = significantPoints
            .GroupBy(x => x.FrequencyBand)
            .OrderByDescending(g => g.Sum(x => x.RelativeErrorPercent))
            .First()
            .Sum(x => x.RelativeErrorPercent) / weightTotal;

        var dominantBand = significantPoints
            .GroupBy(x => x.FrequencyBand)
            .OrderByDescending(g => g.Sum(x => x.RelativeErrorPercent))
            .Select(g => (decimal?)g.Key)
            .FirstOrDefault();

        var cutoffRel = Percentile(allPoints.Select(x => (double)x.RelativeErrorPercent), 0.75);
        var highRelSubset = allPoints
            .Where(x => (double)x.RelativeErrorPercent >= cutoffRel)
            .ToArray();

        double temporalSecondsStd = 0;
        if (highRelSubset.Length > 3)
        {
            var secs = highRelSubset
                .Select(x => x.Timestamp.UtcDateTime.TimeOfDay.TotalSeconds).ToArray();
            temporalSecondsStd = StdDeviation(secs);
        }

        var relErrs = significantPoints.Select(x => (double)x.RelativeErrorPercent).ToArray();
        var coefVar = StdDeviation(relErrs) / Math.Max(relErrs.Average(), 0.001);

        var minuteBucketsElevatedMultiBand =
            ElevatedMultiFrequencyMinuteBuckets(allPoints);

        return new RunInsights(
            ConcentrationDominantBandShareWeighted: weightedTopShare,
            DominantBand: dominantBand,
            TemporalSecondsStdHighRel: (decimal)temporalSecondsStd,
            RelErrorCoefficientOfVariation: (decimal)coefVar,
            MultiBandElevatedBuckets: minuteBucketsElevatedMultiBand);
    }

    private static int ElevatedMultiFrequencyMinuteBuckets(IReadOnlyList<DifferencePoint> allPoints)
    {
        const double relThreshold = 5.0;
        return allPoints
            .Where(x => (double)x.RelativeErrorPercent >= relThreshold)
            .GroupBy(x => new DateTimeOffset(
                x.Timestamp.Year,
                x.Timestamp.Month,
                x.Timestamp.Day,
                x.Timestamp.Hour,
                x.Timestamp.Minute,
                0,
                x.Timestamp.Offset))
            .Count(g => g.Select(p => p.FrequencyBand).Distinct().Count() >= 3);
    }

    private static decimal ResolveDominantBandForVisualization(
        IReadOnlyList<DifferencePoint> significantPoints,
        IReadOnlyList<DifferencePoint> allPoints)
    {
        if (significantPoints.Count > 0)
        {
            return significantPoints
                .GroupBy(x => x.FrequencyBand)
                .OrderByDescending(g => g.Sum(x => x.RelativeErrorPercent))
                .Select(g => g.Key)
                .First();
        }

        return allPoints.Count == 0
            ? 0
            : allPoints
                .GroupBy(x => x.FrequencyBand)
                .OrderByDescending(g => g.Average(x => x.RelativeErrorPercent))
                .Select(g => g.Key)
                .First();
    }

    private static ComparisonVisualizationComputation BuildVisualizationArtifacts(
        IReadOnlyList<AcousticSample> simulationSamples,
        IReadOnlyDictionary<(DateTime, decimal), AcousticSample> fieldLookup,
        IReadOnlyList<DifferencePoint> differences,
        decimal dominantBand)
    {
        var overlay = BuildOverlaySeries(simulationSamples, fieldLookup, dominantBand, maxPoints: 220);
        var heatmap = BuildHeatmapCells(differences, maxCells: 180);
        var clusters = BuildTemporalClusters(differences, maxClusters: 18);
        return new ComparisonVisualizationComputation
        {
            DominantVisualizationFrequencyBand = dominantBand,
            OverlaySeries = overlay,
            HeatmapCells = heatmap,
            TemporalClusters = clusters
        };
    }

    private static IReadOnlyList<OverlaySeriesComputationPoint> BuildOverlaySeries(
        IReadOnlyList<AcousticSample> simulationSamples,
        IReadOnlyDictionary<(DateTime, decimal), AcousticSample> fieldLookup,
        decimal band,
        int maxPoints)
    {
        var pairs = simulationSamples
            .Where(s => s.FrequencyBand == band)
            .OrderBy(s => s.Timestamp)
            .Select(s =>
            {
                if (!fieldLookup.TryGetValue((s.Timestamp.UtcDateTime, s.FrequencyBand), out var field))
                {
                    return (AcousticSample?)null;
                }

                return (AcousticSample?)s;
            })
            .Where(s => s is not null)
            .Cast<AcousticSample>()
            .Select(s =>
            {
                var field = fieldLookup[(s.Timestamp.UtcDateTime, s.FrequencyBand)];
                return new OverlaySeriesComputationPoint
                {
                    Timestamp = s.Timestamp,
                    FrequencyBand = s.FrequencyBand,
                    SimulationDb = s.AmplitudeDb,
                    FieldDb = field.AmplitudeDb
                };
            })
            .ToList();

        if (pairs.Count <= maxPoints)
        {
            return pairs;
        }

        var stride = Math.Max(1, pairs.Count / maxPoints);
        var sampled = new List<OverlaySeriesComputationPoint>();
        for (var i = 0; i < pairs.Count; i += stride)
        {
            sampled.Add(pairs[i]);
        }

        return sampled;
    }

    private static IReadOnlyList<HeatmapComputationCell> BuildHeatmapCells(
        IReadOnlyList<DifferencePoint> differences,
        int maxCells)
    {
        return differences
            .GroupBy(d => new
            {
                Minute = new DateTimeOffset(
                    d.Timestamp.Year,
                    d.Timestamp.Month,
                    d.Timestamp.Day,
                    d.Timestamp.Hour,
                    d.Timestamp.Minute,
                    0,
                    d.Timestamp.Offset),
                d.FrequencyBand
            })
            .Select(g => new HeatmapComputationCell
            {
                TimeBucket = g.Key.Minute.ToString("MM-dd HH:mm"),
                FrequencyBand = g.Key.FrequencyBand,
                MaxRelativeErrorPercent = g.Max(x => x.RelativeErrorPercent)
            })
            .OrderByDescending(x => x.MaxRelativeErrorPercent)
            .Take(maxCells)
            .ToArray();
    }

    private static IReadOnlyList<TemporalDifferenceClusterComputation> BuildTemporalClusters(
        IReadOnlyList<DifferencePoint> differences,
        int maxClusters)
    {
        const double gapMs = 75;
        var ordered = differences
            .OrderByDescending(x => x.RelativeErrorPercent)
            .Take(80)
            .OrderBy(x => x.FrequencyBand)
            .ThenBy(x => x.Timestamp)
            .ToArray();

        var clusters = new List<TemporalDifferenceClusterComputation>();
        foreach (var bandGroup in ordered.GroupBy(x => x.FrequencyBand))
        {
            var points = bandGroup.OrderBy(x => x.Timestamp).ToArray();
            if (points.Length == 0)
            {
                continue;
            }

            var start = 0;
            while (start < points.Length && clusters.Count < maxClusters)
            {
                var end = start;
                while (end + 1 < points.Length
                       && (points[end + 1].Timestamp - points[end].Timestamp).TotalMilliseconds <= gapMs)
                {
                    end++;
                }

                var slice = points[start..(end + 1)];
                clusters.Add(new TemporalDifferenceClusterComputation(
                    Ordinal: 0,
                    TimeStart: slice.Min(x => x.Timestamp),
                    TimeEnd: slice.Max(x => x.Timestamp),
                    FrequencyBand: bandGroup.Key,
                    PointCount: slice.Length,
                    MeanRelativeErrorPercent: decimal.Round(slice.Average(x => x.RelativeErrorPercent), 2)));

                start = end + 1;
            }
        }

        return clusters
            .OrderByDescending(x => x.MeanRelativeErrorPercent)
            .Take(maxClusters)
            .Select((c, i) => c with { Ordinal = i + 1 })
            .ToArray();
    }

    private static IReadOnlyList<Recommendation> BuildRecommendations(
        RunInsights insights,
        decimal mae,
        decimal mre,
        int significantCount,
        decimal significantShare,
        int totalComparedPoints)
    {
        const string method = "rule_engine_v1";
        const string confidenceNote =
            "Confidence is a deterministic interpretability score (0–1) from threshold rules on this run — not an ML probability. "
            + "Higher values mean more independent signals agreed on the same hypothesis.";

        var list = new List<Recommendation>();

        if (insights.ConcentrationDominantBandShareWeighted >= 0.45m
            && significantCount >= 4
            && mre >= 8m)
        {
            var bandLabel = insights.DominantBand is { } b ? $"{b} Hz" : "dominant band";
            var evidence = new List<string>
            {
                $"High mismatch weight concentrated around {bandLabel} (weighted share {insights.ConcentrationDominantBandShareWeighted:P0} of flagged points)",
                $"Global mean relative error is {mre:F2}%",
                TemporalNarrative(insights.TemporalSecondsStdHighRel)
            };

            list.Add(CreateReco(
                "FREQ_BAND_ATTENUATION",
                "FREQUENCY_ATTENUATION",
                "Frequency-specific attenuation or band-limited coupling mismatch suspected.",
                confidenceNote,
                method,
                "Recalibrate band gains, absorption vs frequency, and directional response before full ocean-acoustic rerun.",
                Math.Clamp(0.62m + insights.ConcentrationDominantBandShareWeighted * 0.26m, 0.60m, 0.93m),
                evidence));
        }
        else if (mre >= 18m || (mre >= 12m && insights.ConcentrationDominantBandShareWeighted < 0.35m))
        {
            var evidence = new List<string>
            {
                $"Mean relative error {mre:F2}% spreads across bands (concentration {insights.ConcentrationDominantBandShareWeighted:P0})",
                $"MAE {mae:F3} dB indicates broad amplitude shift, not a single narrowband spike",
                $"Multi-band elevated minutes: {insights.MultiBandElevatedBuckets}"
            };

            list.Add(CreateReco(
                "GLOBAL_MODEL_MISMATCH",
                "MODEL_MISMATCH",
                "Simulation structure diverges from field measurements in a diffuse way.",
                confidenceNote,
                method,
                "Review propagation model inputs (SSP, bathymetry, boundary loss) and compare against field CTD + noise logs.",
                Math.Clamp(0.58m + (mre / 100m) * 1.1m, 0.60m, 0.92m),
                evidence));
        }

        if (mae >= 3m)
        {
            var evidence = new List<string>
            {
                $"MAE {mae:F3} dB exceeds calibration drift guard band",
                $"Relative error coefficient of variation {insights.RelErrorCoefficientOfVariation:F2} among flagged points"
            };

            list.Add(CreateReco(
                "SENSOR_GAIN_BIAS",
                "SENSOR_DRIFT",
                "Consistent amplitude bias consistent with outdated hydrophone/transmitter calibration.",
                confidenceNote,
                method,
                "Validate gain staging with reference tone and reload calibration tables.",
                Math.Clamp(0.61m + (mae / 20m) * 0.25m, 0.61m, 0.91m),
                evidence));
        }

        if (significantShare >= 0.20m && totalComparedPoints > 12)
        {
            var evidence = new List<string>
            {
                $"Significant disagreement on {significantShare:P0} of samples ({significantCount}/{totalComparedPoints})",
                TemporalNarrative(insights.TemporalSecondsStdHighRel),
                $"Elevated minutes span {insights.MultiBandElevatedBuckets} distinct intervals with multi-band activity"
            };

            list.Add(CreateReco(
                "ENVIRONMENT_PROFILE_SHIFT",
                "ENVIRONMENT_VARIANCE",
                "Many localized disagreements often track changing environmental parameters vs the modeled scene.",
                confidenceNote,
                method,
                "Refresh environmental priors (sound speed, surface/bottom loss, noise) and rerun with field truth where available.",
                Math.Clamp(0.55m + significantShare * 0.35m, 0.55m, 0.90m),
                evidence));
        }

        if (insights.MultiBandElevatedBuckets >= 3 && mre is >= 7m and < 19m)
        {
            var evidence = new List<string>
            {
                $"Simultaneous lift on ≥3 bands within the same minute occurred {insights.MultiBandElevatedBuckets} times",
                "Pattern matches broadband interference rather than isolated frequency tilt"
            };

            list.Add(CreateReco(
                "BROADBAND_NOISE_COUPLED",
                "NOISE_INTERFERENCE",
                "Coordinated multi-band residuals point to intermittent noise coupling or episodic masking.",
                confidenceNote,
                method,
                "Inspect duty cycles, towing noise, vessel traffic, and apply adaptive filtering or gating masks.",
                Math.Clamp(0.54m + Math.Min(insights.MultiBandElevatedBuckets, 12) * 0.03m, 0.54m, 0.88m),
                evidence));
        }

        if (list.Count == 0)
        {
            var evidence = new List<string>
            {
                $"Residuals centered (MRE {mre:F2}%) with only {significantShare:P0} significant share",
                "Per-point explanations reference routine geolocation and depth tolerances"
            };

            list.Add(CreateReco(
                "MODEL_WITHIN_VARIANCE",
                "ACCEPTABLE_MODEL",
                "Observed differences stay within expected experimental variance for this dataset.",
                confidenceNote,
                method,
                "Continue monitoring; expand validation coverage before promoting the model.",
                Math.Clamp(0.65m - (significantShare * 0.25m), 0.55m, 0.78m),
                evidence));
        }

        return list;
    }

    private static string TemporalNarrative(decimal temporalStdSeconds)
    {
        return temporalStdSeconds < 120m
            ? $"High-error samples cluster tightly in time (σ ≈ {temporalStdSeconds:F0}s), suggesting structured events"
            : $"High-error samples are temporally diffuse (σ ≈ {temporalStdSeconds:F0}s), suggesting environmental drift";
    }

    private static Recommendation CreateReco(
        string reasonCode,
        string category,
        string explanation,
        string confidenceRationale,
        string inferenceMethod,
        string suggestedAction,
        decimal confidence,
        IReadOnlyList<string> evidence)
    {
        return new Recommendation
        {
            ReasonCode = reasonCode,
            Category = category,
            InferenceMethod = inferenceMethod,
            ConfidenceRationale = confidenceRationale,
            EvidenceSignalsJson = JsonSerializer.Serialize(evidence, RecommendationJson),
            Explanation = explanation,
            SuggestedAction = suggestedAction,
            Confidence = decimal.Round(confidence, 2)
        };
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

    private static string ResolveSeverity(decimal relErrorPercent)
    {
        if (relErrorPercent < 2m)
        {
            return "low";
        }

        if (relErrorPercent < 5m)
        {
            return "moderate";
        }

        if (relErrorPercent < 10m)
        {
            return "high";
        }

        return "critical";
    }

    private static double Percentile(IEnumerable<double> values, double p)
    {
        var arr = values.OrderBy(x => x).ToArray();
        if (arr.Length == 0)
        {
            return 0;
        }

        var idx = (int)Math.Clamp(Math.Floor((arr.Length - 1) * p), 0, arr.Length - 1);
        return arr[idx];
    }

    private static double StdDeviation(IReadOnlyList<double> values)
    {
        if (values.Count < 2)
        {
            return 0;
        }

        var avg = values.Average();
        var sum = values.Sum(x => (x - avg) * (x - avg));
        return Math.Sqrt(sum / (values.Count - 1));
    }

    private sealed record RunInsights(
        decimal ConcentrationDominantBandShareWeighted,
        decimal? DominantBand,
        decimal TemporalSecondsStdHighRel,
        decimal RelErrorCoefficientOfVariation,
        int MultiBandElevatedBuckets);
}
