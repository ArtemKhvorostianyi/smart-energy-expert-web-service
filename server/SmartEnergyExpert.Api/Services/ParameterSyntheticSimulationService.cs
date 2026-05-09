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
        var nameBase = string.IsNullOrWhiteSpace(request.Name)
            ? $"param-simulation-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}"
            : request.Name.Trim();

        var name = await EnsureUniqueDatasetName(dbContext, nameBase, cancellationToken);

        var duration = Math.Clamp(request.DurationMinutes, 1, 240);
        var bands = NormalizeBands(request.FrequencyBandsHz).ToArray();

        var end = DateTimeOffset.UtcNow;
        var start = end.AddMinutes(-duration);

        var bottom = NormalizeBottomType(request.BottomType);
        var rng = BuildRng(request, duration, bands.Length);

        var depth = decimal.Clamp(request.DepthMeters, 1m, 12_000m);
        var tempC = decimal.Clamp(request.TemperatureCelsius, -2m, 40m);
        var salinity = decimal.Clamp(request.SalinityPsu, 0m, 45m);

        var bottomLossDb = BottomAttenuationDb(bottom);
        var depthShelf = DepthSpreadDb(depth);
        var salHue = SalinitySpreadDb(salinity);
        var thermalHue = TemperatureSpreadDb(tempC);
        var noiseRipple = NoiseRippleDb(request.NoiseLevelDb);

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

        for (var minute = 0; minute < duration; minute++)
        {
            var timestamp = start.AddMinutes(minute);
            foreach (var bandHz in bands)
            {
                var rangeMeters = 900m + minute * 18m;

                var frequencyLossDb = FrequencySpreadDb(bandHz);
                var slowDriftDb = SlowTemporalDriftDb(minute);

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
                baseLevel = decimal.Round(baseLevel, 4);

                samples.Add(new AcousticSample
                {
                    Dataset = dataset,
                    Timestamp = timestamp,
                    FrequencyBand = bandHz,
                    AmplitudeDb = baseLevel,
                    DepthMeters = depth + (decimal)(rng.NextDouble() * 1.8 - 0.9),
                    RangeMeters = rangeMeters,
                    SoundSpeed = soundSpeed,
                    NoiseLevelDb = noiseRipple.AmbientField
                });
            }
        }

        dbContext.AcousticSamples.AddRange(samples);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (dataset, samples.Count);
    }

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

    private static decimal FrequencySpreadDb(decimal bandHz)
    {
        var kHzScale = decimal.Max(bandHz / 1000m, 0.18m);
        return decimal.Round(kHzScale * 8.2m + 0.62m * kHzScale * kHzScale + 1.85m, 4);
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
