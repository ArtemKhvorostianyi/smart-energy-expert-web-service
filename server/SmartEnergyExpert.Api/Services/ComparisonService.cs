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

        var simulationBandSet = simulationSamples
            .Select(x => x.FrequencyBand)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
        var fieldBandSet = fieldSamples
            .Select(x => x.FrequencyBand)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
        var bandIntersectionExact = simulationBandSet.Intersect(fieldBandSet).ToArray();
        var bandsAlignable = BandsAlignableAcrossDatasets(simulationBandSet, fieldBandSet);

        var fieldLookupExact = fieldSamples.ToDictionary(
            x => (x.Timestamp.UtcDateTime, x.FrequencyBand),
            x => x,
            EqualityComparer<(DateTime, decimal)>.Default);

        var maxTimeSkew = ResolveMaxPairingTimeSkew(simulationSamples, fieldSamples);
        var timelineAlignment = TimelineAlignment.TryCreate(simulationSamples, fieldSamples);

        var differences = BuildDifferencePoints(
            simulationSamples,
            fieldSamples,
            fieldLookupExact,
            maxTimeSkew);

        Dictionary<Guid, AcousticSample>? experimentProgressPairsBySimId = null;
        var usedExperimentProgressPairing = false;
        if (differences.Count == 0
            && bandsAlignable
            && timelineAlignment is not null)
        {
            experimentProgressPairsBySimId = BuildExperimentProgressPairingMap(
                simulationSamples,
                fieldSamples,
                timelineAlignment);
            differences =
                BuildDifferencePointsFromExperimentProgressPairs(
                    simulationSamples,
                    experimentProgressPairsBySimId);
            usedExperimentProgressPairing = differences.Count > 0;
            if (!usedExperimentProgressPairing)
            {
                experimentProgressPairsBySimId = null;
            }
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
            fieldSamples,
            fieldLookupExact,
            differences,
            dominantBand,
            maxTimeSkew,
            experimentProgressPairsBySimId,
            timelineNormalizationApplied: usedExperimentProgressPairing);

        var recommendations = BuildRecommendations(
            insights,
            mae,
            mre,
            significantCount,
            significantShare,
            comparedPoints,
            simulationSamples.Count,
            fieldSamples.Count,
            simulationBandSet,
            fieldBandSet,
            bandIntersectionExact,
            bandsAlignable,
            usedExperimentProgressPairing && comparedPoints > 0,
            bandsAlignable && timelineAlignment is not null && comparedPoints == 0).ToArray();

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
        IReadOnlyList<AcousticSample> fieldSamples,
        IReadOnlyDictionary<(DateTime, decimal), AcousticSample> fieldLookupExact,
        IReadOnlyList<DifferencePoint> differences,
        decimal dominantBand,
        TimeSpan maxTimeSkew,
        IReadOnlyDictionary<Guid, AcousticSample>? experimentProgressPairsBySimId,
        bool timelineNormalizationApplied)
    {
        var overlay = BuildOverlaySeries(
            simulationSamples,
            fieldSamples,
            fieldLookupExact,
            dominantBand,
            maxTimeSkew,
            experimentProgressPairsBySimId,
            maxPoints: 220);
        var heatmap = BuildHeatmapCells(differences, maxCells: 180);
        var clusters = BuildTemporalClusters(differences, maxClusters: 18);
        return new ComparisonVisualizationComputation
        {
            TimelineNormalizationApplied = timelineNormalizationApplied,
            DominantVisualizationFrequencyBand = dominantBand,
            OverlaySeries = overlay,
            HeatmapCells = heatmap,
            TemporalClusters = clusters
        };
    }

    private static IReadOnlyList<OverlaySeriesComputationPoint> BuildOverlaySeries(
        IReadOnlyList<AcousticSample> simulationSamples,
        IReadOnlyList<AcousticSample> fieldSamples,
        IReadOnlyDictionary<(DateTime, decimal), AcousticSample> fieldLookupExact,
        decimal band,
        TimeSpan maxTimeSkew,
        IReadOnlyDictionary<Guid, AcousticSample>? experimentProgressPairsBySimId,
        int maxPoints)
    {
        var pairs = new List<OverlaySeriesComputationPoint>();
        foreach (var s in simulationSamples.Where(x => x.FrequencyBand == band).OrderBy(x => x.Timestamp))
        {
            AcousticSample? field = null;
            if (experimentProgressPairsBySimId is not null
                && experimentProgressPairsBySimId.TryGetValue(s.Id, out var mapped))
            {
                field = mapped;
            }
            else
            {
                field = ResolveFieldSampleAfterExact(s, fieldSamples, fieldLookupExact, maxTimeSkew);
            }

            if (field is null)
            {
                continue;
            }

            pairs.Add(new OverlaySeriesComputationPoint
            {
                Timestamp = s.Timestamp,
                FrequencyBand = s.FrequencyBand,
                SimulationDb = s.AmplitudeDb,
                FieldDb = field.AmplitudeDb
            });
        }

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
        int totalComparedPoints,
        int simulationSampleCount,
        int fieldSampleCount,
        IReadOnlyList<decimal> simulationBands,
        IReadOnlyList<decimal> fieldBands,
        IReadOnlyList<decimal> bandIntersectionExact,
        bool bandsAlignableAcrossDatasets,
        bool usedExperimentProgressPairing,
        bool attemptedExperimentProgressFallbackWithoutPairs)
    {
        const string method = "rule_engine_v1";
        const string confidenceNote =
            "Confidence is a deterministic interpretability score (0–1) from threshold rules on this run — not an ML probability. "
            + "Higher values mean more independent signals agreed on the same hypothesis.";

        var list = new List<Recommendation>();

        if (totalComparedPoints == 0)
        {
            static string BandHzList(IReadOnlyList<decimal> bands) =>
                bands.Count == 0 ? "(none)" : string.Join(", ", bands.Select(b => b.ToString("G29"))) + " Hz";

            var explainBands = bandIntersectionExact.Count == 0 && !bandsAlignableAcrossDatasets
                ? "Distinct frequency_band sets do not overlap exactly and do not match after common decade scaling (e.g. 6250 Hz vs 62500 Hz)."
                : bandIntersectionExact.Count > 0
                    ? $"Exact band overlap: {BandHzList(bandIntersectionExact)}, but no pairing succeeded across UTC-window and experiment-progress (u-alignment) phases."
                    : bandsAlignableAcrossDatasets switch
                    {
                        true when attemptedExperimentProgressFallbackWithoutPairs =>
                            "Bands align by decade scaling; experiment-progress (u-alignment) pairing ran but assigned no usable pairs (sparse alignable rows or overlapping band coverage missing on one side).",
                        true => "Bands can align by decade scaling, but nearest UTC neighbours still exceeded the adaptive skew cap.",
                        false => "No band alignment after exact match and decade scaling heuristics."
                    };

            var evidence = new List<string>
            {
                $"Simulation: {simulationSampleCount} samples; field: {fieldSampleCount} samples.",
                $"Simulation distinct frequency_band values: {BandHzList(simulationBands)}.",
                $"Field distinct frequency_band values: {BandHzList(fieldBands)}.",
                explainBands,
                "Pairing order: exact (UTC, band) → scaled-band nearest UTC within skew → nearest field neighbour by smallest |u_sim − u_field| where u ∈ [0,1] is normalized elapsed inside each dataset’s own span."
            };

            list.Add(CreateReco(
                "NO_JOINABLE_PAIRS",
                "DATA_ALIGNMENT",
                "Zero paired samples after band scaling, UTC nearest-match, and experiment-progress (u-alignment) pairing — nothing entered the residual statistics.",
                confidenceNote,
                method,
                bandsAlignableAcrossDatasets
                    ? "Provide matching frequency rows on both sides (after unit scaling), ensure each dataset has more than one time instant where needed, and align CSV timelines or epoch references."
                    : "Harmonize frequency_band units between simulator output and field logger (Hz vs kHz-encoding, factor-of-10 columns), then retry.",
                0.95m,
                evidence));

            return list;
        }

        if (usedExperimentProgressPairing)
        {
            var evidence = new List<string>
            {
                "Exact (UTC × band) and UTC-window nearest did not yield pairs.",
                "Each simulation sample u_sim = elapsed fraction in [0,1] inside the simulation CSV span was matched to an alignable field row minimizing |u_field-u_sim| (u_field from the field CSV span independently) when the series are similarly dense; absolute calendar epochs are not matched.",
                "If the field trace is much denser than simulation in the same alignable band (≥8× samples), the prototype places sim rows on evenly spaced time-quantile picks along the ordered field series so every model point samples a different slice of the observation window.",
                $"Compared {totalComparedPoints} residual points; MAE {mae:F3} dB, MRE {mre:F2}%."
            };

            list.Add(CreateReco(
                "EXPERIMENT_PROGRESS_PAIRING",
                "DATA_ALIGNMENT",
                "Automatic pairing used experiment-progress (normalized elapsed share per dataset): valid when both traces represent the same phase structure without a shared UTC baseline.",
                confidenceNote,
                method,
                "If you publish pointwise errors, optionally re-run after importing both series on identical UTC or shared elapsed-second from a common experiment t₀ — exact pairing will supersede progress mode.",
                0.96m,
                evidence));
        }

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

    private static bool BandsAlignableAcrossDatasets(
        IReadOnlyList<decimal> simulationBands,
        IReadOnlyList<decimal> fieldBands) =>
        simulationBands.Any(sb => fieldBands.Any(fb => BandsPhysicallyAlign(sb, fb)));

    /// <summary>Same physical tone within tolerance, allowing common CSV factor-of-10 band encoding.</summary>
    private static bool BandsPhysicallyAlign(decimal simulationBandHz, decimal fieldBandHz)
    {
        if (simulationBandHz <= 0 || fieldBandHz <= 0)
        {
            return false;
        }

        if (simulationBandHz == fieldBandHz)
        {
            return true;
        }

        const decimal absTol = 30m;
        const decimal relTol = 0.05m;
        var direct = Math.Abs(simulationBandHz - fieldBandHz);
        if (direct <= absTol || direct <= Math.Min(simulationBandHz, fieldBandHz) * relTol)
        {
            return true;
        }

        for (var k = -6; k <= 6; k++)
        {
            if (k == 0)
            {
                continue;
            }

            var scaled = simulationBandHz * (decimal)Math.Pow(10.0, k);
            var d = Math.Abs(scaled - fieldBandHz);
            if (d <= absTol || d <= Math.Min(scaled, fieldBandHz) * relTol)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Largest allowed |Δt| for nearest-neighbour pairing after exact key miss.</summary>
    private static TimeSpan ResolveMaxPairingTimeSkew(
        IReadOnlyList<AcousticSample> simulationSamples,
        IReadOnlyList<AcousticSample> fieldSamples)
    {
        if (simulationSamples.Count == 0 || fieldSamples.Count == 0)
        {
            return TimeSpan.FromDays(14);
        }

        var simSpanTicks =
            (simulationSamples.Max(x => x.Timestamp) - simulationSamples.Min(x => x.Timestamp)).Ticks;
        var fieldTicks =
            (fieldSamples.Max(x => x.Timestamp) - fieldSamples.Min(x => x.Timestamp)).Ticks;
        var mergedTicks = Math.Max(simSpanTicks, fieldTicks);

        var halfMerged = mergedTicks / 2;
        var clockSkewFloor = TimeSpan.FromHours(48).Ticks;
        var sixHour = TimeSpan.FromHours(6).Ticks;
        var capTicks = TimeSpan.FromDays(180).Ticks;

        var adaptiveTicks = Math.Max(Math.Max(halfMerged, clockSkewFloor), sixHour);
        adaptiveTicks = Math.Min(adaptiveTicks, capTicks);

        return TimeSpan.FromTicks(adaptiveTicks);
    }

    private static List<DifferencePoint> BuildDifferencePoints(
        IReadOnlyList<AcousticSample> simulationSamples,
        IReadOnlyList<AcousticSample> fieldSamples,
        IReadOnlyDictionary<(DateTime, decimal), AcousticSample> fieldLookupExact,
        TimeSpan maxTimeSkew)
    {
        var differences = new List<DifferencePoint>();
        foreach (var simulation in simulationSamples)
        {
            var field =
                ResolveFieldSampleAfterExact(simulation, fieldSamples, fieldLookupExact, maxTimeSkew);
            var point = ProjectToDifferencePoint(simulation, field);
            if (point is not null)
            {
                differences.Add(point);
            }
        }

        return differences;
    }

    private static List<DifferencePoint> BuildDifferencePointsFromExperimentProgressPairs(
        IReadOnlyList<AcousticSample> simulationSamples,
        IReadOnlyDictionary<Guid, AcousticSample> pairBySimulationId)
    {
        var differences = new List<DifferencePoint>();
        foreach (var simulation in simulationSamples)
        {
            if (!pairBySimulationId.TryGetValue(simulation.Id, out var field))
            {
                continue;
            }

            var point = ProjectToDifferencePoint(simulation, field);
            if (point is not null)
            {
                differences.Add(point);
            }
        }

        return differences;
    }

    private static DifferencePoint? ProjectToDifferencePoint(AcousticSample simulation, AcousticSample? field)
    {
        if (field is null)
        {
            return null;
        }

        var absError = Math.Abs(simulation.AmplitudeDb - field.AmplitudeDb);
        var refMagnitude = Math.Max(Math.Abs(field.AmplitudeDb), 20m);
        var relErrorPercent = Math.Abs(simulation.AmplitudeDb - field.AmplitudeDb) / refMagnitude * 100;
        var severity = ResolveSeverity(relErrorPercent);

        return new DifferencePoint
        {
            Timestamp = simulation.Timestamp,
            FrequencyBand = simulation.FrequencyBand,
            SimulationValue = simulation.AmplitudeDb,
            FieldValue = field.AmplitudeDb,
            AbsoluteError = decimal.Round(absError, 4),
            RelativeErrorPercent = decimal.Round(relErrorPercent, 4),
            Severity = severity,
            Explanation = BuildDifferenceExplanation(simulation, field, relErrorPercent)
        };
    }

    /// <summary>
    /// When the field series is much denser than simulation in the same alignable band, map each sim row to a field row
    /// at evenly spaced quantile indices across the field chronology (balances sample counts). Otherwise use closest u.
    /// </summary>
    private static Dictionary<Guid, AcousticSample> BuildExperimentProgressPairingMap(
        IReadOnlyList<AcousticSample> simulationSamples,
        IReadOnlyList<AcousticSample> fieldSamples,
        TimelineAlignment spanBounds)
    {
        const int stratifyFieldVersusSimRatio = 8;

        var map = new Dictionary<Guid, AcousticSample>();
        foreach (var simBand in simulationSamples.Select(s => s.FrequencyBand).Distinct().OrderBy(x => x))
        {
            var cohortSim = simulationSamples
                .Where(s => s.FrequencyBand == simBand)
                .OrderBy(s => s.Timestamp)
                .ThenBy(s => s.Id)
                .ToList();
            var cohortField = fieldSamples
                .Where(f => BandsPhysicallyAlign(simBand, f.FrequencyBand))
                .OrderBy(f => f.Timestamp)
                .ThenBy(f => f.Id)
                .ToList();
            if (cohortField.Count == 0 || cohortSim.Count == 0)
            {
                continue;
            }

            var stratifyQuantiles =
                cohortField.Count >= stratifyFieldVersusSimRatio * Math.Max(cohortSim.Count, 1);

            for (var i = 0; i < cohortSim.Count; i++)
            {
                AcousticSample paired;
                if (stratifyQuantiles)
                {
                    var fi = cohortField.Count == 1
                        ? 0
                        : (int)Math.Round(i * (cohortField.Count - 1) / (double)Math.Max(cohortSim.Count - 1, 1));
                    fi = Math.Clamp(fi, 0, cohortField.Count - 1);
                    paired = cohortField[fi];
                }
                else
                {
                    var hit = FindBestExperimentProgressFieldPair(cohortSim[i], cohortField, spanBounds);
                    if (hit is null)
                    {
                        continue;
                    }

                    paired = hit;
                }

                map[cohortSim[i].Id] = paired;
            }
        }

        return map;
    }

    private static AcousticSample? ResolveFieldSampleAfterExact(
        AcousticSample simulation,
        IReadOnlyList<AcousticSample> fieldSamples,
        IReadOnlyDictionary<(DateTime, decimal), AcousticSample> exactLookup,
        TimeSpan maxTimeSkew)
    {
        if (exactLookup.TryGetValue((simulation.Timestamp.UtcDateTime, simulation.FrequencyBand), out var hit))
        {
            return hit;
        }

        return FindBestFlexibleFieldPair(simulation, fieldSamples, maxTimeSkew);
    }

    private static AcousticSample? FindBestFlexibleFieldPair(
        AcousticSample simulation,
        IReadOnlyList<AcousticSample> fieldSamples,
        TimeSpan maxTimeSkew)
    {
        AcousticSample? best = null;
        var bestSeconds = double.MaxValue;
        var windowSec = maxTimeSkew.TotalSeconds;

        foreach (var f in fieldSamples)
        {
            if (!BandsPhysicallyAlign(simulation.FrequencyBand, f.FrequencyBand))
            {
                continue;
            }

            var dt = Math.Abs((f.Timestamp - simulation.Timestamp).TotalSeconds);
            if (dt > windowSec)
            {
                continue;
            }

            if (dt < bestSeconds)
            {
                bestSeconds = dt;
                best = f;
            }
        }

        return best;
    }

    /// <summary>Field sample on an alignable band whose normalized elapsed u is closest to this simulation sample’s u.</summary>
    private static AcousticSample? FindBestExperimentProgressFieldPair(
        AcousticSample simulation,
        IReadOnlyList<AcousticSample> fieldSamples,
        TimelineAlignment spanBounds)
    {
        var uSim = UnitProgressAlongDatasetSpan(simulation.Timestamp, spanBounds.SimMin, spanBounds.SimMax);

        AcousticSample? best = null;
        var bestDelta = decimal.MaxValue;
        foreach (var field in fieldSamples)
        {
            if (!BandsPhysicallyAlign(simulation.FrequencyBand, field.FrequencyBand))
            {
                continue;
            }

            var uField =
                UnitProgressAlongDatasetSpan(field.Timestamp, spanBounds.FieldMin, spanBounds.FieldMax);
            var d = Math.Abs(uSim - uField);
            if (d > bestDelta)
            {
                continue;
            }

            if (best is null || d < bestDelta)
            {
                bestDelta = d;
                best = field;
            }
        }

        return best;
    }

    /// <summary>Normalized position of <paramref name="instant"/> inside [<paramref name="min"/>, <paramref name="max"/>], in [0,1], independent clock — only relative span.</summary>
    private static decimal UnitProgressAlongDatasetSpan(
        DateTimeOffset instant,
        DateTimeOffset min,
        DateTimeOffset max)
    {
        var minU = min.ToUniversalTime();
        var maxU = max.ToUniversalTime();
        var instU = instant.ToUniversalTime();
        var denomTicks = Math.Max((maxU - minU).Ticks, 1L);
        var numTicks = (instU - minU).Ticks;
        var clampedTicks = Math.Min(Math.Max(numTicks, 0L), denomTicks);
        return clampedTicks / (decimal)denomTicks;
    }

    /// <summary>Per-dataset time span extents for experiment-progress pairing (normalized u per side).</summary>
    private sealed record TimelineAlignment(
        DateTimeOffset SimMin,
        DateTimeOffset SimMax,
        DateTimeOffset FieldMin,
        DateTimeOffset FieldMax)
    {
        internal static TimelineAlignment? TryCreate(
            IReadOnlyList<AcousticSample> simulationSamples,
            IReadOnlyList<AcousticSample> fieldSamples)
        {
            if (simulationSamples.Count == 0 || fieldSamples.Count == 0)
            {
                return null;
            }

            return new TimelineAlignment(
                simulationSamples.Min(static x => x.Timestamp),
                simulationSamples.Max(static x => x.Timestamp),
                fieldSamples.Min(static x => x.Timestamp),
                fieldSamples.Max(static x => x.Timestamp));
        }
    }

    private sealed record RunInsights(
        decimal ConcentrationDominantBandShareWeighted,
        decimal? DominantBand,
        decimal TemporalSecondsStdHighRel,
        decimal RelErrorCoefficientOfVariation,
        int MultiBandElevatedBuckets);
}
