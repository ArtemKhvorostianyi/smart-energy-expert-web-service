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
}
