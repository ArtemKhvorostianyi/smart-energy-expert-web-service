using Microsoft.EntityFrameworkCore;
using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.DTOs;
using SmartEnergyExpert.Api.Entities;

namespace SmartEnergyExpert.Api.Services;

/// <summary>
/// Heuristic forward model for DSS demos — not calibrated ocean acoustics.
/// Writes <see cref="AcousticSample"/> rows compatible with comparison and CSV workflows.
/// </summary>
public sealed class ParameterSyntheticSimulationService : IParameterSyntheticSimulationService
{
    private static readonly decimal[] DefaultBandsHz = [200m, 400m, 800m, 1200m, 6250m];

    public async Task<(Dataset Dataset, int SampleCount)> GenerateAndPersistAsync(
        AppDbContext dbContext,
        GenerateSimulationDatasetRequest request,
        CancellationToken cancellationToken)
    {
        if (request.AlignToFieldDatasetId is { } fieldId)
        {
            return await GenerateAlignedToFieldDatasetAsync(dbContext, request, fieldId, cancellationToken);
        }

        var nameBase = string.IsNullOrWhiteSpace(request.Name)
            ? $"param-simulation-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}"
            : request.Name.Trim();

        var name = await EnsureUniqueDatasetNameAsync(dbContext, nameBase, cancellationToken);

        var duration = Math.Clamp(request.DurationMinutes, 1, 240);
        var bands = NormalizeBands(request.FrequencyBandsHz).ToArray();

        var end = DateTimeOffset.UtcNow;
        var start = end.AddMinutes(-duration);

        var bottom = NormalizeBottomType(request.BottomType);
        var rng = BuildRng(request, duration, bands.Length);

        var depth = decimal.Clamp(request.DepthMeters, 1m, 12_000m);
        var tempC = decimal.Clamp(request.TemperatureCelsius, -2m, 40m);
        var salinity = decimal.Clamp(request.SalinityPsu, 0m, 45m);

        var dataset = new Dataset
        {
            Name = name,
            Type = "simulation",
            SourceSystem = "parameter-synthetic",
            Version = string.IsNullOrWhiteSpace(request.ModelVersion)
                ? "env-heuristic-v1"
                : request.ModelVersion.Trim(),
            TimeRangeStart = start,
            TimeRangeEnd = end,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Datasets.Add(dataset);

        var samples = new List<AcousticSample>(Math.Max(16, duration * bands.Length));
        var soundSpeed = ApproximateSoundSpeedMps(tempC, salinity, depth);
        var noiseRippleAmbient = NoiseRippleDb(request.NoiseLevelDb);

        for (var minute = 0; minute < duration; minute++)
        {
            var timestamp = start.AddMinutes(minute);
            foreach (var bandHz in bands)
            {
                var rangeMeters = 900m + minute * 18m;

                var baseLevel =
                    ComposeHeuristicSplDb(bandHz, minute, depth, bottom, tempC, salinity, rng, noiseRippleAmbient);

                samples.Add(new AcousticSample
                {
                    Dataset = dataset,
                    Timestamp = timestamp,
                    FrequencyBand = bandHz,
                    AmplitudeDb = baseLevel,
                    DepthMeters = depth + (decimal)(rng.NextDouble() * 1.8 - 0.9),
                    RangeMeters = rangeMeters,
                    SoundSpeed = soundSpeed,
                    NoiseLevelDb = noiseRippleAmbient.AmbientField
                });
            }
        }

        dbContext.AcousticSamples.AddRange(samples);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (dataset, samples.Count);
    }

    private async Task<(Dataset Dataset, int SampleCount)> GenerateAlignedToFieldDatasetAsync(
        AppDbContext dbContext,
        GenerateSimulationDatasetRequest request,
        Guid fieldDatasetId,
        CancellationToken cancellationToken)
    {
        var fieldMeta = await dbContext.Datasets.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == fieldDatasetId, cancellationToken);
        if (fieldMeta is null)
        {
            throw new InvalidOperationException("Align target dataset was not found.");
        }

        if (!string.Equals(fieldMeta.Type, "field", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Align target dataset must have type \"field\".");
        }

        var rows = await dbContext.AcousticSamples.AsNoTracking()
            .Where(x => x.DatasetId == fieldDatasetId)
            .OrderBy(x => x.Timestamp)
            .ThenBy(x => x.FrequencyBand)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            throw new InvalidOperationException("Align target has no acoustic samples.");
        }

        var minTs = rows.Min(x => x.Timestamp);
        var maxTs = rows.Max(x => x.Timestamp);
        var spanMinutes = Math.Max(1d, (maxTs - minTs).TotalMinutes);
        var durationForRng = (int)Math.Clamp(Math.Ceiling(spanMinutes), 1, 240);

        var envelopeForRng = new GenerateSimulationDatasetRequest
        {
            Name = request.Name,
            DepthMeters = request.DepthMeters,
            TemperatureCelsius = request.TemperatureCelsius,
            SalinityPsu = request.SalinityPsu,
            NoiseLevelDb = request.NoiseLevelDb,
            BottomType = request.BottomType,
            DurationMinutes = durationForRng,
            FrequencyBandsHz = request.FrequencyBandsHz,
            ModelVersion = request.ModelVersion,
            AlignToFieldDatasetId = null
        };

        var rng = CreateSyntheticRandomSeeded(envelopeForRng, durationForRng, 1);

        var nameBase = string.IsNullOrWhiteSpace(request.Name)
            ? $"param-simulation-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}"
            : request.Name.Trim();
        var name = await EnsureUniqueDatasetNameAsync(dbContext, nameBase, cancellationToken);

        var bottom = NormalizeBottomType(request.BottomType);
        var tempC = decimal.Clamp(request.TemperatureCelsius, -2m, 40m);
        var salinity = decimal.Clamp(request.SalinityPsu, 0m, 45m);
        var defaultDepth = decimal.Clamp(request.DepthMeters, 1m, 12_000m);
        var noiseRippleAmbient = NoiseRippleDb(request.NoiseLevelDb);

        var dataset = new Dataset
        {
            Name = name,
            Type = "simulation",
            SourceSystem = "parameter-synthetic-field-aligned",
            Version = string.IsNullOrWhiteSpace(request.ModelVersion)
                ? "env-heuristic-v1"
                : request.ModelVersion.Trim(),
            TimeRangeStart = minTs,
            TimeRangeEnd = maxTs,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Datasets.Add(dataset);

        var surrogateSpan = Math.Max(0, durationForRng - 1);
        var heuristics = new decimal[rows.Count];
        var sumField = 0m;
        var sumHeuristic = 0m;

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var surrogateMinute = rows.Count <= 1
                ? 0
                : (int)Math.Round(i * surrogateSpan / (double)(rows.Count - 1));
            surrogateMinute = Math.Clamp(surrogateMinute, 0, durationForRng - 1);

            var depthSample =
                row.DepthMeters > 0 ? decimal.Clamp(row.DepthMeters, 1m, 12_000m) : defaultDepth;

            var h = ComposeHeuristicSplDb(
                row.FrequencyBand,
                surrogateMinute,
                depthSample,
                bottom,
                tempC,
                salinity,
                rng,
                noiseRippleAmbient);
            heuristics[i] = h;
            sumField += row.AmplitudeDb;
            sumHeuristic += h;
        }

        // Heuristic dB is illustrative; field CSV is on another absolute scale. Shift so means match — residuals
        // reflect shape/timing mismatches rather than hundreds of dB systematic bias vs field.
        var meanOffsetDb =
            rows.Count > 0 ? decimal.Round((sumField - sumHeuristic) / rows.Count, 6) : 0m;

        var samples = new List<AcousticSample>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var surrogateMinute = rows.Count <= 1
                ? 0
                : (int)Math.Round(i * surrogateSpan / (double)(rows.Count - 1));
            surrogateMinute = Math.Clamp(surrogateMinute, 0, durationForRng - 1);
            var depthSample =
                row.DepthMeters > 0 ? decimal.Clamp(row.DepthMeters, 1m, 12_000m) : defaultDepth;
            var soundSpeed =
                row.SoundSpeed ?? ApproximateSoundSpeedMps(tempC, salinity, depthSample);

            samples.Add(new AcousticSample
            {
                Dataset = dataset,
                Timestamp = row.Timestamp,
                FrequencyBand = row.FrequencyBand,
                AmplitudeDb = decimal.Round(heuristics[i] + meanOffsetDb, 4),
                DepthMeters = depthSample,
                RangeMeters = row.RangeMeters,
                SoundSpeed = soundSpeed,
                NoiseLevelDb = row.NoiseLevelDb ?? noiseRippleAmbient.AmbientField
            });
        }

        dbContext.AcousticSamples.AddRange(samples);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (dataset, samples.Count);
    }

    /// <inheritdoc />
    public decimal EstimateAmplitudeDb(
        decimal frequencyBandHz,
        int surrogateMinuteIndex,
        GenerateSimulationDatasetRequest envelope,
        Random rng)
    {
        var duration = Math.Clamp(envelope.DurationMinutes, 1, 240);
        var minute = Math.Clamp(surrogateMinuteIndex, 0, duration - 1);
        var depth = decimal.Clamp(envelope.DepthMeters, 1m, 12_000m);
        var tempC = decimal.Clamp(envelope.TemperatureCelsius, -2m, 40m);
        var salinity = decimal.Clamp(envelope.SalinityPsu, 0m, 45m);
        var bottom = NormalizeBottomType(envelope.BottomType);
        var ripple = NoiseRippleDb(envelope.NoiseLevelDb);
        return ComposeHeuristicSplDb(frequencyBandHz, minute, depth, bottom, tempC, salinity, rng, ripple);
    }

    private static decimal ComposeHeuristicSplDb(
        decimal frequencyBandHz,
        int surrogateMinuteWithinDuration,
        decimal depthMeters,
        string normalizedBottomType,
        decimal tempCelsius,
        decimal salinityPsu,
        Random rng,
        NoiseRipple noiseRipple)
    {
        var bottomLossDb = BottomAttenuationDb(normalizedBottomType);
        var depthShelf = DepthSpreadDb(depthMeters);
        var thermalHue = TemperatureSpreadDb(tempCelsius);
        var salHue = SalinitySpreadDb(salinityPsu);
        return ComposeHeuristicSplDbInner(
            frequencyBandHz,
            surrogateMinuteWithinDuration,
            bottomLossDb,
            depthShelf,
            thermalHue,
            salHue,
            rng,
            noiseRipple);
    }

    private static decimal ComposeHeuristicSplDbInner(
        decimal frequencyBandHz,
        int surrogateMinuteWithinDuration,
        decimal bottomLossDb,
        decimal depthShelf,
        decimal thermalHue,
        decimal salHue,
        Random rng,
        NoiseRipple noiseRipple)
    {
        var frequencyLossDb = FrequencySpreadDb(frequencyBandHz);
        var slowDriftDb = SlowTemporalDriftDb(surrogateMinuteWithinDuration);
        var baseLevel =
            -68m
            - bottomLossDb
            - frequencyLossDb
            + depthShelf
            + thermalHue
            + salHue
            + slowDriftDb
            + Ripple(rng);

        baseLevel -= noiseRipple.JitterAmp * (decimal)rng.NextDouble();
        return decimal.Round(baseLevel, 4);
    }

    internal static Random CreateSyntheticRandomSeeded(
        GenerateSimulationDatasetRequest envelope,
        int durationMinutesClamp,
        int bandCardinality)
    {
        var duration = Math.Clamp(durationMinutesClamp, 1, 240);
        return BuildRng(envelope, duration, bandCardinality);
    }

    internal static Task<string> EnsureUniqueDatasetNameAsync(
        AppDbContext dbContext,
        string nameBase,
        CancellationToken cancellationToken) =>
        EnsureUniqueDatasetName(dbContext, nameBase, cancellationToken);

    private static NoiseRipple NoiseRippleDb(decimal noiseAmbientDbRe1uPa)
    {
        var n = noiseAmbientDbRe1uPa;
        var jitter = decimal.Clamp((-92m - n) * 0.45m + 12m, 2m, 18m);
        var ambient = decimal.Round(decimal.Clamp(n, -120m, -20m), 4);
        return new NoiseRipple(ambient, jitter);
    }

    private readonly record struct NoiseRipple(decimal AmbientField, decimal JitterAmp);

    private static IEnumerable<decimal> NormalizeBands(decimal[]? bands)
    {
        if (bands is { Length: > 0 })
        {
            var cleaned = bands
                .Where(x => x is > 0 and < 500_000m)
                .Distinct()
                .OrderBy(x => x)
                .Take(48)
                .ToArray();

            return cleaned.Length > 0 ? cleaned : DefaultBandsHz;
        }

        return DefaultBandsHz;
    }

    private static async Task<string> EnsureUniqueDatasetName(
        AppDbContext db,
        string name,
        CancellationToken cancellationToken)
    {
        var candidate = name;
        var suffix = 0;
        while (await db.Datasets.AsNoTracking().AnyAsync(x => x.Name == candidate, cancellationToken))
        {
            suffix++;
            candidate = $"{name}-{suffix:D4}";
            if (suffix > 20)
            {
                candidate = $"{name}-{Guid.NewGuid():N}";
                break;
            }
        }

        return candidate;
    }

    private static Random BuildRng(
        GenerateSimulationDatasetRequest req,
        int duration,
        int bandCount)
    {
        var seed = HashCode.Combine(
            req.Name,
            req.DepthMeters,
            req.TemperatureCelsius,
            req.SalinityPsu,
            req.NoiseLevelDb,
            NormalizeBottomType(req.BottomType),
            duration,
            bandCount);
        return new Random(seed);
    }

    private static string NormalizeBottomType(string? bottom)
    {
        if (string.IsNullOrWhiteSpace(bottom))
        {
            return "sand";
        }

        return bottom.Trim().ToLowerInvariant()
            .Replace(' ', '_')
            .Replace("-", "_");
    }

    private static decimal BottomAttenuationDb(string normalizedBottom) =>
        normalizedBottom switch
        {
            "hard_rock" or "granite" or "rock" => 0.85m,
            "sand" or "sand_silt" => 2.35m,
            "mud" or "clay_mud" => 4.1m,
            "silt" => 6.05m,
            "soft_bottom" => 7.35m,
            _ => 2.95m
        };

    private static decimal DepthSpreadDb(decimal depthMeters) =>
        decimal.Round(-Math.Max(0m, depthMeters - 20m) * 0.012m, 4);

    /// <summary>
    /// Extra “loss” vs a notional LF reference — must stay bounded for HF band columns stored in Hz
    /// (e.g. sonar centroid 62 500 Hz would previously blow up ~kHz² to thousands of dB and break comparisons).
    /// </summary>
    private static decimal FrequencySpreadDb(decimal bandHz)
    {
        var kHzRaw = decimal.Max(bandHz / 1000m, 0.18m);
        var kHzScale = decimal.Min(kHzRaw, 10m);
        var curved = decimal.Round(kHzScale * 8.2m + 0.62m * kHzScale * kHzScale + 1.85m, 4);
        const decimal maxFrequencyPenaltyDb = 85m;
        return decimal.Min(curved, maxFrequencyPenaltyDb);
    }

    private static decimal SalinitySpreadDb(decimal salinityPsu) =>
        decimal.Round((salinityPsu - 34.5m) * 0.18m, 4);

    private static decimal TemperatureSpreadDb(decimal tempCelsius) =>
        decimal.Round((tempCelsius - 12m) * 0.42m, 4);

    private static decimal SlowTemporalDriftDb(int minute) =>
        (decimal)Math.Sin(minute / 15d) * 3.25m;

    private static decimal Ripple(Random rnd) =>
        (decimal)(rnd.NextDouble() * 5.8 - 2.85);

    /// <summary>Light empirical SSP proxy (°C, PSU, metres).</summary>
    private static decimal ApproximateSoundSpeedMps(decimal tempCelsius, decimal salinityPsu, decimal depthMeters) =>
        decimal.Round(
            1448.96m + depthMeters * 0.0165m + tempCelsius * 4.591m +
            (salinityPsu - 35m) * 1.341m -
            decimal.Max(0m, tempCelsius) * decimal.Max(0m, tempCelsius) * 0.051m +
            tempCelsius * tempCelsius * tempCelsius * 0.00023m,
            4);
}
