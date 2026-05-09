using Microsoft.EntityFrameworkCore;
using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.Entities;
using SmartEnergyExpert.Api.Services;
using Xunit;

namespace SmartEnergyExpert.Api.Tests;

public sealed class ComparisonServiceTests
{
    [Fact]
    public async Task CompareAsync_ReturnsMetricsDifferencesAndRecommendations()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"cmp-{Guid.NewGuid()}")
            .Options;

        await using var db = new AppDbContext(dbOptions);
        var simulation = new Dataset
        {
            Name = "sim",
            Type = "simulation",
            SourceSystem = "test",
            Version = "v1",
            TimeRangeStart = DateTimeOffset.UtcNow,
            TimeRangeEnd = DateTimeOffset.UtcNow.AddMinutes(2)
        };
        var field = new Dataset
        {
            Name = "field",
            Type = "field",
            SourceSystem = "test",
            Version = "v1",
            TimeRangeStart = DateTimeOffset.UtcNow,
            TimeRangeEnd = DateTimeOffset.UtcNow.AddMinutes(2)
        };
        db.Datasets.AddRange(simulation, field);

        var t0 = DateTimeOffset.UtcNow;
        db.AcousticSamples.AddRange(
            new AcousticSample { Dataset = simulation, Timestamp = t0, FrequencyBand = 400, AmplitudeDb = -70, DepthMeters = 50, RangeMeters = 1000 },
            new AcousticSample { Dataset = field, Timestamp = t0, FrequencyBand = 400, AmplitudeDb = -58, DepthMeters = 50, RangeMeters = 1000 },
            new AcousticSample { Dataset = simulation, Timestamp = t0.AddMinutes(1), FrequencyBand = 800, AmplitudeDb = -72, DepthMeters = 50, RangeMeters = 1100 },
            new AcousticSample { Dataset = field, Timestamp = t0.AddMinutes(1), FrequencyBand = 800, AmplitudeDb = -69, DepthMeters = 50, RangeMeters = 1100 });
        await db.SaveChangesAsync();

        var service = new ComparisonService(db);
        var result = await service.CompareAsync(simulation, field, topN: 10, CancellationToken.None);

        Assert.True(result.Mae > 0);
        Assert.True(result.Rmse > 0);
        Assert.Equal(2, result.TotalComparedPoints);
        Assert.NotEmpty(result.TopDifferences);
        Assert.NotEmpty(result.Recommendations);
    }

    [Fact]
    public async Task CompareAsync_PairsWhenUtcEpochsDivergeButBandsAndProgressAlign()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"cmp-tl-{Guid.NewGuid()}")
            .Options;

        await using var db = new AppDbContext(dbOptions);
        var simulation = new Dataset
        {
            Name = "sim",
            Type = "simulation",
            SourceSystem = "test",
            Version = "v1",
            TimeRangeStart = DateTimeOffset.Parse("2020-01-01T00:00:00Z"),
            TimeRangeEnd = DateTimeOffset.Parse("2020-01-01T03:00:00Z")
        };
        var field = new Dataset
        {
            Name = "field",
            Type = "field",
            SourceSystem = "test",
            Version = "v1",
            TimeRangeStart = DateTimeOffset.Parse("2025-08-02T09:15:05Z"),
            TimeRangeEnd = DateTimeOffset.Parse("2025-08-02T09:15:06Z")
        };
        db.Datasets.AddRange(simulation, field);

        var simT0 = DateTimeOffset.Parse("2020-01-01T00:00:00Z");
        var simT1 = DateTimeOffset.Parse("2020-01-01T01:00:00Z");
        var fldT0 = DateTimeOffset.Parse("2025-08-02T09:15:05.030Z");
        var fldT1 = DateTimeOffset.Parse("2025-08-02T09:15:05.770Z");

        db.AcousticSamples.AddRange(
            new AcousticSample { Dataset = simulation, Timestamp = simT0, FrequencyBand = 6250, AmplitudeDb = -71, DepthMeters = 50, RangeMeters = 900 },
            new AcousticSample { Dataset = simulation, Timestamp = simT1, FrequencyBand = 6250, AmplitudeDb = -69, DepthMeters = 50, RangeMeters = 900 },
            new AcousticSample { Dataset = field, Timestamp = fldT0, FrequencyBand = 62500, AmplitudeDb = -60, DepthMeters = 50, RangeMeters = 900 },
            new AcousticSample { Dataset = field, Timestamp = fldT1, FrequencyBand = 62500, AmplitudeDb = -62, DepthMeters = 50, RangeMeters = 900 });

        await db.SaveChangesAsync();

        var service = new ComparisonService(db);
        var result = await service.CompareAsync(simulation, field, topN: 10, CancellationToken.None);

        Assert.Equal(2, result.TotalComparedPoints);
        Assert.True(result.Mae > 0);
        Assert.True(result.Visualization.TimelineNormalizationApplied);
        Assert.Contains(
            result.Recommendations,
            static r => string.Equals(r.ReasonCode, "EXPERIMENT_PROGRESS_PAIRING", StringComparison.Ordinal));
        Assert.True(result.Recommendations.Count >= 1);
    }

    /// <summary>Ensures sparse simulation + dense field maps one sim row per evenly spaced field quantile (not all to one cluster).</summary>
    [Fact]
    public async Task CompareAsync_UsesTimeQuantilesWhenFieldMuchDenserThanSimulation()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"cmp-q-{Guid.NewGuid()}")
            .Options;

        await using var db = new AppDbContext(dbOptions);
        var simulation = new Dataset
        {
            Name = "sim-sparse",
            Type = "simulation",
            SourceSystem = "test",
            Version = "v1",
            TimeRangeStart = DateTimeOffset.Parse("2019-06-01T00:00:00Z"),
            TimeRangeEnd = DateTimeOffset.Parse("2019-06-01T01:00:00Z")
        };
        var field = new Dataset
        {
            Name = "field-dense",
            Type = "field",
            SourceSystem = "test",
            Version = "v1",
            TimeRangeStart = DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
            TimeRangeEnd = DateTimeOffset.Parse("2024-01-01T00:00:03Z")
        };
        db.Datasets.AddRange(simulation, field);

        var simTs = new[]
        {
            DateTimeOffset.Parse("2019-06-01T00:05:00Z"),
            DateTimeOffset.Parse("2019-06-01T00:30:00Z"),
            DateTimeOffset.Parse("2019-06-01T00:55:00Z")
        };
        const decimal simBandHz = 6200m;

        for (var i = 0; i < simTs.Length; i++)
        {
            db.AcousticSamples.Add(new AcousticSample
            {
                Dataset = simulation,
                Timestamp = simTs[i],
                FrequencyBand = simBandHz,
                AmplitudeDb = -80m - i,
                DepthMeters = 40,
                RangeMeters = 900
            });
        }

        var baseT = DateTimeOffset.Parse("2024-01-01T00:00:00Z");
        const decimal fieldBandHz = 62000m;

        for (var k = 0; k < 30; k++)
        {
            db.AcousticSamples.Add(new AcousticSample
            {
                Dataset = field,
                Timestamp = baseT.AddMilliseconds(k * 80),
                FrequencyBand = fieldBandHz,
                AmplitudeDb = -50m + k * 0.1m,
                DepthMeters = 40,
                RangeMeters = 900
            });
        }

        await db.SaveChangesAsync();

        var service = new ComparisonService(db);
        var result = await service.CompareAsync(simulation, field, topN: 10, CancellationToken.None);

        Assert.Equal(3, result.TotalComparedPoints);
        Assert.True(result.Visualization.TimelineNormalizationApplied);
    }
}
