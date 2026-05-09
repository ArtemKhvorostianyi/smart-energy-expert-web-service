using Microsoft.EntityFrameworkCore;
using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.Entities;
using SmartEnergyExpert.Api.Services;
using Xunit;

namespace SmartEnergyExpert.Api.Tests;

/// <summary>Імпорт CSV зі сторінки «Керування датасетами» — <see cref="AcousticCsvBatchImporter"/>.</summary>
public sealed class AcousticCsvBatchImporterTests
{
    private static async Task<(AppDbContext db, Dataset ds)> CreateEmptyDatasetAsync()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"csv-{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(opts);
        var ds = new Dataset
        {
            Name = "import-target",
            Type = "field",
            SourceSystem = "csv-import",
            Version = "v1",
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Datasets.Add(ds);
        await db.SaveChangesAsync();
        return (db, ds);
    }

    [Fact]
    public async Task Import_skips_header_row_and_imports_data_rows()
    {
        var (db, ds) = await CreateEmptyDatasetAsync();
        await using (db)
        {
            var csv = """
                timestamp,frequency_band_hz,amplitude_db,depth_m,range_m,sound_speed,noise_db
                2024-06-15T10:00:00Z,400,-70.5,50,1000,1490,-88
                2024-06-15T10:01:00Z,800,-72.25,51,1010,1491,-89
                """;

            var n = await AcousticCsvBatchImporter.ImportIntoDatasetAsync(db, ds, csv);

            Assert.Equal(2, n);
            Assert.Equal(2, await db.AcousticSamples.CountAsync(x => x.DatasetId == ds.Id));

            var tracked = await db.Datasets.SingleAsync(x => x.Id == ds.Id);
            Assert.Equal(DateTimeOffset.Parse("2024-06-15T10:00:00Z"), tracked.TimeRangeStart);
            Assert.Equal(DateTimeOffset.Parse("2024-06-15T10:01:00Z"), tracked.TimeRangeEnd);
        }
    }

    [Fact]
    public async Task Import_skips_short_or_invalid_lines_without_failing()
    {
        var (db, ds) = await CreateEmptyDatasetAsync();
        await using (db)
        {
            var csv = """
                timestamp,frequency_band_hz,amplitude_db,depth_m,range_m,sound_speed,noise_db
                not-a-date,400,-70,50,1000,1490,-88
                2024-01-02T00:00:00Z,200,-71,50,1000,1490,-88
                a,b
                """;

            var n = await AcousticCsvBatchImporter.ImportIntoDatasetAsync(db, ds, csv);

            Assert.Equal(1, n);
            Assert.Single(await db.AcousticSamples.Where(x => x.DatasetId == ds.Id).ToListAsync());
        }
    }

    [Fact]
    public async Task Import_optional_sound_speed_and_noise_cells_parse_as_null_when_invalid()
    {
        var (db, ds) = await CreateEmptyDatasetAsync();
        await using (db)
        {
            var csv = """
                timestamp,frequency_band_hz,amplitude_db,depth_m,range_m,sound_speed,noise_db
                2024-03-01T12:00:00Z,1200,-65,40,900,not-a-number,also-bad
                """;

            var n = await AcousticCsvBatchImporter.ImportIntoDatasetAsync(db, ds, csv);

            Assert.Equal(1, n);
            var row = await db.AcousticSamples.SingleAsync(x => x.DatasetId == ds.Id);
            Assert.Null(row.SoundSpeed);
            Assert.Null(row.NoiseLevelDb);
        }
    }
}
