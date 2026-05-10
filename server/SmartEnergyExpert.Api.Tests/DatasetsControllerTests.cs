using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEnergyExpert.Api.Controllers;
using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.DTOs;
using SmartEnergyExpert.Api.Entities;
using Xunit;

namespace SmartEnergyExpert.Api.Tests;

/// <summary>Unit-тести API «Керування датасетами» — <see cref="DatasetsController"/> + in-memory БД.</summary>
public sealed class DatasetsControllerTests
{
    private static AppDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options);

    private static DatasetsController CreateController(AppDbContext db) => new(db);

    [Fact]
    public async Task GetAll_returns_datasets_ordered_by_time_range_start_descending()
    {
        var dbName = $"ds-ctrl-all-{Guid.NewGuid():N}";
        await using var db = CreateDb(dbName);
        var older = DateTimeOffset.Parse("2020-01-01T00:00:00Z");
        var newer = DateTimeOffset.Parse("2024-07-01T00:00:00Z");
        db.Datasets.AddRange(
            new Dataset
            {
                Name = "old",
                Type = "field",
                SourceSystem = "t",
                Version = "v1",
                TimeRangeStart = older,
                TimeRangeEnd = older.AddHours(1),
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new Dataset
            {
                Name = "new",
                Type = "field",
                SourceSystem = "t",
                Version = "v1",
                TimeRangeStart = newer,
                TimeRangeEnd = newer.AddHours(1),
                UpdatedAt = DateTimeOffset.UtcNow
            });
        await db.SaveChangesAsync();

        var sut = CreateController(db);
        var result = await sut.GetAll(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<DatasetResponse>>(ok.Value);
        Assert.Equal(2, list.Count);
        Assert.Equal("new", list[0].Name);
        Assert.Equal("old", list[1].Name);
    }

    [Fact]
    public async Task Create_without_name_returns_bad_request()
    {
        await using var db = CreateDb($"ds-create-bad-{Guid.NewGuid():N}");
        var sut = CreateController(db);
        var result = await sut.Create(
            new CreateDatasetRequest { Name = "   ", Type = "field", SourceSystem = "csv-import" },
            CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_persists_lowercased_type_and_returns_sample_count_zero()
    {
        await using var db = CreateDb($"ds-create-ok-{Guid.NewGuid():N}");
        var sut = CreateController(db);
        var result = await sut.Create(
            new CreateDatasetRequest
            {
                Name = "My Field",
                Type = "FIELD",
                SourceSystem = "csv-import",
                Version = "v2"
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<DatasetResponse>(ok.Value);
        Assert.Equal("field", dto.Type);
        Assert.Equal("My Field", dto.Name);
        Assert.Equal(0, dto.SampleCount);

        var entity = await db.Datasets.SingleAsync(x => x.Id == dto.Id);
        Assert.Equal("field", entity.Type);
    }

    [Fact]
    public async Task Delete_removes_dataset_and_linked_comparison_runs()
    {
        await using var db = CreateDb($"ds-del-{Guid.NewGuid():N}");
        var sim = new Dataset
        {
            Name = "sim",
            Type = "simulation",
            SourceSystem = "t",
            Version = "v1",
            TimeRangeStart = DateTimeOffset.UtcNow,
            TimeRangeEnd = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var fld = new Dataset
        {
            Name = "fld",
            Type = "field",
            SourceSystem = "t",
            Version = "v1",
            TimeRangeStart = DateTimeOffset.UtcNow,
            TimeRangeEnd = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Datasets.AddRange(sim, fld);
        await db.SaveChangesAsync();

        db.ComparisonRuns.Add(new ComparisonRun
        {
            SimulationDatasetId = sim.Id,
            FieldDatasetId = fld.Id,
            Mae = 1,
            Rmse = 1,
            MeanRelativeErrorPercent = 1,
            P95AbsoluteError = 1,
            TotalComparedPoints = 1,
            SignificantDifferenceCount = 0
        });
        await db.SaveChangesAsync();

        var sut = CreateController(db);
        var del = await sut.Delete(sim.Id, CancellationToken.None);
        Assert.IsType<NoContentResult>(del);

        Assert.Empty(await db.Datasets.Where(x => x.Id == sim.Id).ToListAsync());
        Assert.Empty(await db.ComparisonRuns.Where(x => x.SimulationDatasetId == sim.Id).ToListAsync());
        Assert.Single(await db.Datasets.Where(x => x.Id == fld.Id).ToListAsync());
    }

    [Fact]
    public async Task Delete_unknown_id_returns_not_found()
    {
        await using var db = CreateDb($"ds-del-miss-{Guid.NewGuid():N}");
        var sut = CreateController(db);
        var result = await sut.Delete(Guid.NewGuid(), CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetSignalOverview_unknown_dataset_returns_not_found()
    {
        await using var db = CreateDb($"ds-ov-miss-{Guid.NewGuid():N}");
        var sut = CreateController(db);
        var result = await sut.GetSignalOverview(Guid.NewGuid(), CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetSignalOverview_empty_samples_returns_zero_aggregates()
    {
        await using var db = CreateDb($"ds-ov-empty-{Guid.NewGuid():N}");
        var ds = new Dataset
        {
            Name = "empty",
            Type = "field",
            SourceSystem = "t",
            Version = "v1",
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Datasets.Add(ds);
        await db.SaveChangesAsync();

        var sut = CreateController(db);
        var result = await sut.GetSignalOverview(ds.Id, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var overview = Assert.IsType<DatasetSignalOverviewResponse>(ok.Value);
        Assert.Equal(0, overview.SampleCount);
        Assert.Equal(0, overview.DistinctFrequencyBins);
    }

    [Fact]
    public async Task GetSignalOverview_computes_duration_and_frequency_span()
    {
        await using var db = CreateDb($"ds-ov-full-{Guid.NewGuid():N}");
        var ds = new Dataset
        {
            Name = "with-samples",
            Type = "field",
            SourceSystem = "t",
            Version = "v1",
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Datasets.Add(ds);
        await db.SaveChangesAsync();

        var t0 = DateTimeOffset.Parse("2025-01-01T00:00:00Z");
        var t1 = DateTimeOffset.Parse("2025-01-01T00:00:10Z");
        db.AcousticSamples.AddRange(
            new AcousticSample
            {
                DatasetId = ds.Id,
                Timestamp = t0,
                FrequencyBand = 200m,
                AmplitudeDb = -70m,
                DepthMeters = 50m,
                RangeMeters = 1000m,
                NoiseLevelDb = -90m
            },
            new AcousticSample
            {
                DatasetId = ds.Id,
                Timestamp = t1,
                FrequencyBand = 800m,
                AmplitudeDb = -65m,
                DepthMeters = 50m,
                RangeMeters = 1000m,
                NoiseLevelDb = -91m
            });
        await db.SaveChangesAsync();

        var sut = CreateController(db);
        var result = await sut.GetSignalOverview(ds.Id, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var overview = Assert.IsType<DatasetSignalOverviewResponse>(ok.Value);
        Assert.Equal(2, overview.SampleCount);
        Assert.Equal(2, overview.DistinctFrequencyBins);
        Assert.Equal(200m, overview.FrequencyMinHz);
        Assert.Equal(800m, overview.FrequencyMaxHz);
        Assert.True(overview.DurationSeconds > 0);
        Assert.Equal(-65m, overview.PeakAmplitudeDb);
    }

    [Fact]
    public async Task ImportCsv_empty_body_returns_bad_request()
    {
        await using var db = CreateDb($"ds-csv-empty-{Guid.NewGuid():N}");
        var ds = new Dataset
        {
            Name = "t",
            Type = "field",
            SourceSystem = "t",
            Version = "v1",
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Datasets.Add(ds);
        await db.SaveChangesAsync();

        var sut = CreateController(db);
        var result = await sut.ImportCsv(ds.Id, "   \n\t  ", CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task ImportCsv_imports_rows_via_controller()
    {
        await using var db = CreateDb($"ds-csv-ok-{Guid.NewGuid():N}");
        var ds = new Dataset
        {
            Name = "csv-ds",
            Type = "field",
            SourceSystem = "csv-import",
            Version = "v1",
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Datasets.Add(ds);
        await db.SaveChangesAsync();

        var csv = """
            timestamp,frequency_band_hz,amplitude_db,depth_m,range_m,sound_speed,noise_db
            2024-08-01T08:00:00Z,500,-68,45,950,1488,-87
            """;

        var sut = CreateController(db);
        var result = await sut.ImportCsv(ds.Id, csv, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(ok.Value);
        Assert.Equal(1, await db.AcousticSamples.CountAsync(x => x.DatasetId == ds.Id));
    }

    [Fact]
    public async Task GetSamplesPage_clamps_limit_and_returns_slice()
    {
        await using var db = CreateDb($"ds-page-{Guid.NewGuid():N}");
        var ds = new Dataset
        {
            Name = "page",
            Type = "field",
            SourceSystem = "t",
            Version = "v1",
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Datasets.Add(ds);
        await db.SaveChangesAsync();

        var baseT = DateTimeOffset.Parse("2023-05-05T00:00:00Z");
        for (var i = 0; i < 5; i++)
        {
            db.AcousticSamples.Add(new AcousticSample
            {
                DatasetId = ds.Id,
                Timestamp = baseT.AddSeconds(i),
                FrequencyBand = 400m,
                AmplitudeDb = -60m - i,
                DepthMeters = 50m,
                RangeMeters = 1000m
            });
        }

        await db.SaveChangesAsync();

        var sut = CreateController(db);
        var result = await sut.GetSamplesPage(ds.Id, offset: 1, limit: 2, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var page = Assert.IsType<DatasetSamplesPageResponse>(ok.Value);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(1, page.Offset);
        Assert.Equal(2, page.Limit);
        Assert.Equal(2, page.Items.Count);
    }
}
