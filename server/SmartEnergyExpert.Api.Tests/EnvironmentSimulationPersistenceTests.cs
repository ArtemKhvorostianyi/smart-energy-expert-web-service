using Microsoft.EntityFrameworkCore;
using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.DTOs;
using SmartEnergyExpert.Api.Entities;
using SmartEnergyExpert.Api.Services;
using Xunit;

namespace SmartEnergyExpert.Api.Tests;

/// <summary>
/// Unit-тести для «Симуляції на основі середовища» — <see cref="ParameterSyntheticSimulationService.GenerateAndPersistAsync"/>
/// (незалежна сітка та віддзеркалення поля).
/// </summary>
public sealed class EnvironmentSimulationPersistenceTests
{
    [Fact]
    public async Task IndependentGrid_persists_duration_times_band_count_samples()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"env-sim-grid-{Guid.NewGuid():N}")
            .Options;
        await using var db = new AppDbContext(opts);

        const int duration = 3;
        const int defaultBandCount = 5;
        var sut = new ParameterSyntheticSimulationService();
        var (_, count) = await sut.GenerateAndPersistAsync(
            db,
            new GenerateSimulationDatasetRequest
            {
                Name = "grid-mini",
                DurationMinutes = duration,
                DepthMeters = 60m,
                TemperatureCelsius = 12m,
                SalinityPsu = 35m,
                NoiseLevelDb = -92m,
                BottomType = "sand"
            },
            CancellationToken.None);

        Assert.Equal(duration * defaultBandCount, count);

        var dataset = await db.Datasets.AsNoTracking().SingleAsync(x => x.Name == "grid-mini");
        Assert.Equal("simulation", dataset.Type);
        Assert.Equal("parameter-synthetic", dataset.SourceSystem);
        Assert.True(dataset.TimeRangeEnd > dataset.TimeRangeStart);

        var distinctMinutes = await db.AcousticSamples.AsNoTracking()
            .Where(x => x.DatasetId == dataset.Id)
            .Select(x => x.Timestamp)
            .Distinct()
            .CountAsync();
        Assert.Equal(duration, distinctMinutes);
    }

    [Fact]
    public async Task IndependentGrid_custom_frequency_bands_override_defaults()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"env-sim-bands-{Guid.NewGuid():N}")
            .Options;
        await using var db = new AppDbContext(opts);

        var bands = new[] { 1500m, 3000m, 4500m };
        const int duration = 2;
        var sut = new ParameterSyntheticSimulationService();
        var (_, count) = await sut.GenerateAndPersistAsync(
            db,
            new GenerateSimulationDatasetRequest
            {
                Name = "custom-bands",
                DurationMinutes = duration,
                FrequencyBandsHz = bands,
                DepthMeters = 40m,
                TemperatureCelsius = 10m,
                SalinityPsu = 34m,
                NoiseLevelDb = -90m,
                BottomType = "mud"
            },
            CancellationToken.None);

        Assert.Equal(duration * bands.Length, count);

        var ds = await db.Datasets.AsNoTracking().SingleAsync(x => x.Name == "custom-bands");
        var storedBands = await db.AcousticSamples.AsNoTracking()
            .Where(x => x.DatasetId == ds.Id)
            .Select(x => x.FrequencyBand)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();
        Assert.Equal(bands.OrderBy(x => x), storedBands);
    }

    [Fact]
    public async Task Second_generation_with_same_base_name_gets_numeric_suffix()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"env-sim-dup-{Guid.NewGuid():N}")
            .Options;
        await using var db = new AppDbContext(opts);

        var sut = new ParameterSyntheticSimulationService();
        var req = new GenerateSimulationDatasetRequest
        {
            Name = "shared-name",
            DurationMinutes = 1,
            DepthMeters = 50m,
            TemperatureCelsius = 12m,
            SalinityPsu = 35m,
            NoiseLevelDb = -92m,
            BottomType = "sand"
        };

        await sut.GenerateAndPersistAsync(db, req, CancellationToken.None);
        await sut.GenerateAndPersistAsync(db, req, CancellationToken.None);

        var names = await db.Datasets.AsNoTracking()
            .Select(x => x.Name)
            .OrderBy(x => x)
            .ToListAsync();

        Assert.Equal(2, names.Count);
        Assert.Contains("shared-name", names);
        Assert.Contains("shared-name-0001", names);
    }

    [Fact]
    public async Task AlignToField_throws_when_target_dataset_missing()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"env-sim-miss-{Guid.NewGuid():N}")
            .Options;
        await using var db = new AppDbContext(opts);

        var sut = new ParameterSyntheticSimulationService();
        var missingId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GenerateAndPersistAsync(
                db,
                new GenerateSimulationDatasetRequest
                {
                    Name = "orphan-align",
                    AlignToFieldDatasetId = missingId,
                    DurationMinutes = 5,
                    DepthMeters = 60m,
                    TemperatureCelsius = 12m,
                    SalinityPsu = 35m,
                    NoiseLevelDb = -92m,
                    BottomType = "sand"
                },
                CancellationToken.None));

        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AlignToField_throws_when_target_is_not_field_type()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"env-sim-wrongtype-{Guid.NewGuid():N}")
            .Options;
        await using var db = new AppDbContext(opts);

        var simulation = new Dataset
        {
            Name = "not-a-field",
            Type = "simulation",
            SourceSystem = "test",
            Version = "v1",
            TimeRangeStart = DateTimeOffset.UtcNow,
            TimeRangeEnd = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Datasets.Add(simulation);
        await db.SaveChangesAsync();

        var sut = new ParameterSyntheticSimulationService();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GenerateAndPersistAsync(
                db,
                new GenerateSimulationDatasetRequest
                {
                    Name = "bad-align",
                    AlignToFieldDatasetId = simulation.Id,
                    DepthMeters = 60m,
                    TemperatureCelsius = 12m,
                    SalinityPsu = 35m,
                    NoiseLevelDb = -92m,
                    BottomType = "sand"
                },
                CancellationToken.None));

        Assert.Contains("field", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AlignToField_throws_when_field_has_no_samples()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"env-sim-emptyfield-{Guid.NewGuid():N}")
            .Options;
        await using var db = new AppDbContext(opts);

        var field = new Dataset
        {
            Name = "empty-field",
            Type = "field",
            SourceSystem = "test",
            Version = "v1",
            TimeRangeStart = DateTimeOffset.UtcNow,
            TimeRangeEnd = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Datasets.Add(field);
        await db.SaveChangesAsync();

        var sut = new ParameterSyntheticSimulationService();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GenerateAndPersistAsync(
                db,
                new GenerateSimulationDatasetRequest
                {
                    Name = "align-empty",
                    AlignToFieldDatasetId = field.Id,
                    DepthMeters = 60m,
                    TemperatureCelsius = 12m,
                    SalinityPsu = 35m,
                    NoiseLevelDb = -92m,
                    BottomType = "sand"
                },
                CancellationToken.None));

        Assert.Contains("no acoustic", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IndependentGrid_each_sample_has_positive_range_and_sound_speed()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"env-sim-geo-{Guid.NewGuid():N}")
            .Options;
        await using var db = new AppDbContext(opts);

        var sut = new ParameterSyntheticSimulationService();
        await sut.GenerateAndPersistAsync(
            db,
            new GenerateSimulationDatasetRequest
            {
                Name = "geo-check",
                DurationMinutes = 2,
                DepthMeters = 55m,
                TemperatureCelsius = 15m,
                SalinityPsu = 36m,
                NoiseLevelDb = -88m,
                BottomType = "sand"
            },
            CancellationToken.None);

        var geoDs = await db.Datasets.AsNoTracking().SingleAsync(x => x.Name == "geo-check");
        var samples = await db.AcousticSamples.AsNoTracking()
            .Where(x => x.DatasetId == geoDs.Id)
            .ToListAsync();

        Assert.NotEmpty(samples);
        Assert.All(samples, s =>
        {
            Assert.True(s.RangeMeters > 0);
            Assert.True(s.SoundSpeed is > 1400m and < 1600m);
            Assert.True(s.DepthMeters > 0);
        });
    }
}
