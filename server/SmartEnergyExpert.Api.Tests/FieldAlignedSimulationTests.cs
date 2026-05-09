using Microsoft.EntityFrameworkCore;
using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.DTOs;
using SmartEnergyExpert.Api.Entities;
using SmartEnergyExpert.Api.Services;
using Xunit;

namespace SmartEnergyExpert.Api.Tests;

public sealed class FieldAlignedSimulationTests
{
    [Fact]
    public async Task AlignToField_preservesUtc_grid_so_comparison_is_exact_pairs()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var db = new AppDbContext(opts);

        var field = new Dataset
        {
            Name = "imported-arlut-field-test",
            Type = "field",
            SourceSystem = "csv-import",
            Version = "v1",
            TimeRangeStart = DateTimeOffset.Parse("2030-01-01T00:00:00Z"),
            TimeRangeEnd = DateTimeOffset.Parse("2030-01-01T00:00:00Z"),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Datasets.Add(field);
        await db.SaveChangesAsync();

        db.AcousticSamples.AddRange(
            new AcousticSample
            {
                Dataset = field,
                Timestamp = DateTimeOffset.Parse("2030-01-01T00:00:00.000Z"),
                FrequencyBand = 62500m,
                AmplitudeDb = -10m,
                DepthMeters = 40m,
                RangeMeters = 100m,
                SoundSpeed = 1485m,
                NoiseLevelDb = -90m
            },
            new AcousticSample
            {
                Dataset = field,
                Timestamp = DateTimeOffset.Parse("2030-01-01T00:00:00.050Z"),
                FrequencyBand = 62500m,
                AmplitudeDb = -11m,
                DepthMeters = 40m,
                RangeMeters = 101m,
                SoundSpeed = 1485m,
                NoiseLevelDb = -90m
            },
            new AcousticSample
            {
                Dataset = field,
                Timestamp = DateTimeOffset.Parse("2030-01-01T00:00:00.099Z"),
                FrequencyBand = 62500m,
                AmplitudeDb = -12m,
                DepthMeters = 40m,
                RangeMeters = 102m,
                SoundSpeed = 1485m,
                NoiseLevelDb = -90m
            });

        await db.SaveChangesAsync();

        var sut = new ParameterSyntheticSimulationService();
        var (_, simCount) = await sut.GenerateAndPersistAsync(
            db,
            new GenerateSimulationDatasetRequest
            {
                Name = "aligned-sim-mini",
                AlignToFieldDatasetId = field.Id,
                DepthMeters = 40m,
                TemperatureCelsius = 12m,
                SalinityPsu = 35m,
                NoiseLevelDb = -90m,
                BottomType = "sand",
                DurationMinutes = 999,
                FrequencyBandsHz = [100m],
                ModelVersion = "tests"
            },
            CancellationToken.None);

        Assert.Equal(3, simCount);

        var gridField = await db.AcousticSamples.AsNoTracking()
            .Where(x => x.DatasetId == field.Id)
            .OrderBy(x => x.Timestamp)
            .ThenBy(x => x.FrequencyBand)
            .Select(x => $"{x.Timestamp:o}|{x.FrequencyBand}")
            .ToListAsync();

        var simDataset =
            await db.Datasets.AsNoTracking().SingleAsync(x => x.Name == "aligned-sim-mini");
        Assert.Equal("parameter-synthetic-field-aligned", simDataset.SourceSystem);

        var gridSim = await db.AcousticSamples.AsNoTracking()
            .Where(x => x.DatasetId == simDataset.Id)
            .OrderBy(x => x.Timestamp)
            .ThenBy(x => x.FrequencyBand)
            .Select(x => $"{x.Timestamp:o}|{x.FrequencyBand}")
            .ToListAsync();

        Assert.Equal(gridField, gridSim);

        var simTracked = await db.Datasets.SingleAsync(x => x.Id == simDataset.Id);

        var fieldTracked = await db.Datasets.SingleAsync(x => x.Id == field.Id);
        var compare = await new ComparisonService(db).CompareAsync(
            simTracked,
            fieldTracked,
            topN: 10,
            CancellationToken.None);

        Assert.Equal(3, compare.TotalComparedPoints);
        Assert.False(compare.Visualization.TimelineNormalizationApplied);
        Assert.True(compare.Mae > 0);
    }
}
