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

    /// <summary>Гідроакустичне порівняння: порожні вибірки не дають спарованих точок і нульових агрегатів.</summary>
    [Fact]
    public async Task CompareAsync_WhenBothDatasetsHaveNoSamples_ReturnsZeroComparedPointsAndZeroMetrics()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"cmp-empty-{Guid.NewGuid()}")
            .Options;

        await using var db = new AppDbContext(dbOptions);
        var simulation = new Dataset
        {
            Name = "sim-empty",
            Type = "simulation",
            SourceSystem = "test",
            Version = "v1",
            TimeRangeStart = DateTimeOffset.UtcNow,
            TimeRangeEnd = DateTimeOffset.UtcNow
        };
        var field = new Dataset
        {
            Name = "field-empty",
            Type = "field",
            SourceSystem = "test",
            Version = "v1",
            TimeRangeStart = DateTimeOffset.UtcNow,
            TimeRangeEnd = DateTimeOffset.UtcNow
        };
        db.Datasets.AddRange(simulation, field);
        await db.SaveChangesAsync();

        var service = new ComparisonService(db);
        var result = await service.CompareAsync(simulation, field, topN: 10, CancellationToken.None);

        Assert.Equal(0, result.TotalComparedPoints);
        Assert.Equal(0, result.Mae);
        Assert.Equal(0, result.Rmse);
        Assert.Equal(0, result.MeanRelativeErrorPercent);
        Assert.Equal(0, result.SignificantDifferenceCount);
        Assert.Empty(result.TopDifferences);
        Assert.NotEmpty(result.Recommendations);
    }

    /// <summary>Точний збіг UTC × смуга × дБ дає нульові залишки (MRE залежить від формули відносної помилки).</summary>
    [Fact]
    public async Task CompareAsync_WhenAmplitudesMatchExactly_MaeIsZero()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"cmp-match-{Guid.NewGuid()}")
            .Options;

        await using var db = new AppDbContext(dbOptions);
        var simulation = new Dataset
        {
            Name = "sim",
            Type = "simulation",
            SourceSystem = "test",
            Version = "v1",
            TimeRangeStart = DateTimeOffset.Parse("2024-05-01T10:00:00Z"),
            TimeRangeEnd = DateTimeOffset.Parse("2024-05-01T10:05:00Z")
        };
        var field = new Dataset
        {
            Name = "field",
            Type = "field",
            SourceSystem = "test",
            Version = "v1",
            TimeRangeStart = DateTimeOffset.Parse("2024-05-01T10:00:00Z"),
            TimeRangeEnd = DateTimeOffset.Parse("2024-05-01T10:05:00Z")
        };
        db.Datasets.AddRange(simulation, field);

        var t = DateTimeOffset.Parse("2024-05-01T10:02:00Z");
        const decimal band = 800m;
        const decimal dbVal = -63.5m;
        db.AcousticSamples.AddRange(
            new AcousticSample
            {
                Dataset = simulation,
                Timestamp = t,
                FrequencyBand = band,
                AmplitudeDb = dbVal,
                DepthMeters = 50,
                RangeMeters = 1000
            },
            new AcousticSample
            {
                Dataset = field,
                Timestamp = t,
                FrequencyBand = band,
                AmplitudeDb = dbVal,
                DepthMeters = 50,
                RangeMeters = 1000
            });
        await db.SaveChangesAsync();

        var service = new ComparisonService(db);
        var result = await service.CompareAsync(simulation, field, topN: 5, CancellationToken.None);

        Assert.Equal(1, result.TotalComparedPoints);
        Assert.Equal(0, result.Mae);
        Assert.Equal(0, result.Rmse);
        Assert.Equal(0, result.MeanRelativeErrorPercent);
        Assert.Equal(0, result.SignificantDifferenceCount);
        Assert.False(result.Visualization.TimelineNormalizationApplied);
    }

    /// <summary>Смуги без спільного фізичного узгодження (без масштабу десятка в межах допуску) — пар немає.</summary>
    [Fact]
    public async Task CompareAsync_WhenFrequencyBandsDoNotAlign_NoComparedPoints()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"cmp-bands-{Guid.NewGuid()}")
            .Options;

        await using var db = new AppDbContext(dbOptions);
        var simulation = new Dataset
        {
            Name = "sim",
            Type = "simulation",
            SourceSystem = "test",
            Version = "v1",
            TimeRangeStart = DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
            TimeRangeEnd = DateTimeOffset.Parse("2024-01-01T00:10:00Z")
        };
        var field = new Dataset
        {
            Name = "field",
            Type = "field",
            SourceSystem = "test",
            Version = "v1",
            TimeRangeStart = DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
            TimeRangeEnd = DateTimeOffset.Parse("2024-01-01T00:10:00Z")
        };
        db.Datasets.AddRange(simulation, field);

        var t = DateTimeOffset.Parse("2024-01-01T00:05:00Z");
        db.AcousticSamples.AddRange(
            new AcousticSample
            {
                Dataset = simulation,
                Timestamp = t,
                FrequencyBand = 1000m,
                AmplitudeDb = -70,
                DepthMeters = 50,
                RangeMeters = 1000
            },
            new AcousticSample
            {
                Dataset = field,
                Timestamp = t,
                FrequencyBand = 2010m,
                AmplitudeDb = -60,
                DepthMeters = 50,
                RangeMeters = 1000
            });
        await db.SaveChangesAsync();

        var service = new ComparisonService(db);
        var result = await service.CompareAsync(simulation, field, topN: 10, CancellationToken.None);

        Assert.Equal(0, result.TotalComparedPoints);
        Assert.Empty(result.TopDifferences);
        Assert.NotEmpty(result.Recommendations);
    }

    /// <summary>Різні кодування смуги (×10) при спільному UTC — гнучке спаровування без нормалізації прогресу.</summary>
    [Fact]
    public async Task CompareAsync_WhenDecadeScaledBandsMatchSameUtc_PairsWithoutTimelineNormalization()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"cmp-decade-{Guid.NewGuid()}")
            .Options;

        await using var db = new AppDbContext(dbOptions);
        var simulation = new Dataset
        {
            Name = "sim",
            Type = "simulation",
            SourceSystem = "test",
            Version = "v1",
            TimeRangeStart = DateTimeOffset.Parse("2024-06-01T12:00:00Z"),
            TimeRangeEnd = DateTimeOffset.Parse("2024-06-01T12:01:00Z")
        };
        var field = new Dataset
        {
            Name = "field",
            Type = "field",
            SourceSystem = "test",
            Version = "v1",
            TimeRangeStart = DateTimeOffset.Parse("2024-06-01T12:00:00Z"),
            TimeRangeEnd = DateTimeOffset.Parse("2024-06-01T12:01:00Z")
        };
        db.Datasets.AddRange(simulation, field);

        var t = DateTimeOffset.Parse("2024-06-01T12:00:30Z");
        db.AcousticSamples.AddRange(
            new AcousticSample
            {
                Dataset = simulation,
                Timestamp = t,
                FrequencyBand = 6250m,
                AmplitudeDb = -71,
                DepthMeters = 50,
                RangeMeters = 900
            },
            new AcousticSample
            {
                Dataset = field,
                Timestamp = t,
                FrequencyBand = 62500m,
                AmplitudeDb = -60,
                DepthMeters = 50,
                RangeMeters = 900
            });
        await db.SaveChangesAsync();

        var service = new ComparisonService(db);
        var result = await service.CompareAsync(simulation, field, topN: 5, CancellationToken.None);

        Assert.Equal(1, result.TotalComparedPoints);
        Assert.False(result.Visualization.TimelineNormalizationApplied);
        Assert.True(result.Mae > 0);
    }

    /// <summary>Top-N обмежує довжину списку найбільших відносних відхилень (після сортування).</summary>
    [Fact]
    public async Task CompareAsync_RespectsTopN_OnManyPairedPoints()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"cmp-topn-{Guid.NewGuid()}")
            .Options;

        await using var db = new AppDbContext(dbOptions);
        var simulation = new Dataset
        {
            Name = "sim",
            Type = "simulation",
            SourceSystem = "test",
            Version = "v1",
            TimeRangeStart = DateTimeOffset.Parse("2023-01-01T00:00:00Z"),
            TimeRangeEnd = DateTimeOffset.Parse("2023-01-01T01:00:00Z")
        };
        var field = new Dataset
        {
            Name = "field",
            Type = "field",
            SourceSystem = "test",
            Version = "v1",
            TimeRangeStart = DateTimeOffset.Parse("2023-01-01T00:00:00Z"),
            TimeRangeEnd = DateTimeOffset.Parse("2023-01-01T01:00:00Z")
        };
        db.Datasets.AddRange(simulation, field);

        const decimal band = 400m;
        var baseT = DateTimeOffset.Parse("2023-01-01T00:00:00Z");
        for (var i = 0; i < 12; i++)
        {
            var ts = baseT.AddSeconds(i);
            db.AcousticSamples.Add(new AcousticSample
            {
                Dataset = simulation,
                Timestamp = ts,
                FrequencyBand = band,
                AmplitudeDb = -40m - i * 3m,
                DepthMeters = 50,
                RangeMeters = 1000
            });
            db.AcousticSamples.Add(new AcousticSample
            {
                Dataset = field,
                Timestamp = ts,
                FrequencyBand = band,
                AmplitudeDb = -50m,
                DepthMeters = 50,
                RangeMeters = 1000
            });
        }

        await db.SaveChangesAsync();

        var service = new ComparisonService(db);
        const int topN = 4;
        var result = await service.CompareAsync(simulation, field, topN, CancellationToken.None);

        Assert.Equal(12, result.TotalComparedPoints);
        Assert.Equal(topN, result.TopDifferences.Count);
        for (var i = 0; i < result.TopDifferences.Count - 1; i++)
        {
            Assert.True(
                result.TopDifferences[i].RelativeErrorPercent >= result.TopDifferences[i + 1].RelativeErrorPercent);
        }
    }

    /// <summary>Контракт: при topN=0 движок усе одно повертає хоча б один найгірший ряд (Math.Max(1, topN)).</summary>
    [Fact]
    public async Task CompareAsync_WhenTopNIsZero_ReturnsSingleTopDifference()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"cmp-top0-{Guid.NewGuid()}")
            .Options;

        await using var db = new AppDbContext(dbOptions);
        var simulation = new Dataset
        {
            Name = "sim",
            Type = "simulation",
            SourceSystem = "test",
            Version = "v1",
            TimeRangeStart = DateTimeOffset.Parse("2022-01-01T00:00:00Z"),
            TimeRangeEnd = DateTimeOffset.Parse("2022-01-01T00:05:00Z")
        };
        var field = new Dataset
        {
            Name = "field",
            Type = "field",
            SourceSystem = "test",
            Version = "v1",
            TimeRangeStart = DateTimeOffset.Parse("2022-01-01T00:00:00Z"),
            TimeRangeEnd = DateTimeOffset.Parse("2022-01-01T00:05:00Z")
        };
        db.Datasets.AddRange(simulation, field);

        var t0 = DateTimeOffset.Parse("2022-01-01T00:01:00Z");
        var t1 = DateTimeOffset.Parse("2022-01-01T00:02:00Z");
        db.AcousticSamples.AddRange(
            new AcousticSample
            {
                Dataset = simulation,
                Timestamp = t0,
                FrequencyBand = 500m,
                AmplitudeDb = -55,
                DepthMeters = 50,
                RangeMeters = 1000
            },
            new AcousticSample
            {
                Dataset = field,
                Timestamp = t0,
                FrequencyBand = 500m,
                AmplitudeDb = -50,
                DepthMeters = 50,
                RangeMeters = 1000
            },
            new AcousticSample
            {
                Dataset = simulation,
                Timestamp = t1,
                FrequencyBand = 500m,
                AmplitudeDb = -80,
                DepthMeters = 50,
                RangeMeters = 1000
            },
            new AcousticSample
            {
                Dataset = field,
                Timestamp = t1,
                FrequencyBand = 500m,
                AmplitudeDb = -50,
                DepthMeters = 50,
                RangeMeters = 1000
            });
        await db.SaveChangesAsync();

        var service = new ComparisonService(db);
        var result = await service.CompareAsync(simulation, field, topN: 0, CancellationToken.None);

        Assert.Equal(2, result.TotalComparedPoints);
        Assert.Single(result.TopDifferences);
        Assert.Equal(t1, result.TopDifferences[0].Timestamp);
        Assert.True(result.TopDifferences[0].AbsoluteError > 20m);
    }
}
