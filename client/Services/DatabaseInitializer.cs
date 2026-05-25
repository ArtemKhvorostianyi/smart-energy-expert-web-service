using Microsoft.EntityFrameworkCore;
using SmartEnergyExpert.Client.Data;
using SmartEnergyExpert.Client.Entities;

namespace SmartEnergyExpert.Client.Services;

public sealed class DatabaseInitializer(IDbContextFactory<AppDbContext> dbFactory)
{
    internal const string BundledArlutPartAFieldDatasetName = "ARLUT 01 part A field stride2500";
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var dbContext = await dbFactory.CreateDbContextAsync(cancellationToken);

            await dbContext.Database.MigrateAsync(cancellationToken);
            await SeedSyntheticDatasetsAsync(dbContext, cancellationToken);

            await SeedBundledArlutPartAFieldDatasetAsync(dbContext, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[DatabaseInitializer] Skipped: {ex.Message}. Ensure PostgreSQL is running and ConnectionStrings:DefaultConnection is set.");
        }
    }

    private static async Task SeedSyntheticDatasetsAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        if (await dbContext.Datasets.AnyAsync(cancellationToken))
        {
            return;
        }

        var start = DateTimeOffset.UtcNow.AddHours(-3);
        var simulation = new Dataset
        {
            Name = "synthetic-simulation-v1",
            Type = "simulation",
            SourceSystem = "synthetic-generator",
            Version = "v1",
            TimeRangeStart = start,
            TimeRangeEnd = start.AddMinutes(59)
        };
        var field = new Dataset
        {
            Name = "synthetic-field-v1",
            Type = "field",
            SourceSystem = "synthetic-generator",
            Version = "v1",
            TimeRangeStart = start,
            TimeRangeEnd = start.AddMinutes(59)
        };

        dbContext.Datasets.AddRange(simulation, field);

        var random = new Random(42);
        var bands = new[] { 200m, 400m, 800m, 1200m };
        var simulationSamples = new List<AcousticSample>();
        var fieldSamples = new List<AcousticSample>();
        for (var minute = 0; minute < 60; minute++)
        {
            var timestamp = start.AddMinutes(minute);
            foreach (var band in bands)
            {
                var baseAmplitude = -72m + (band / 1000m) + (decimal)Math.Sin(minute / 12d) * 4m;
                var simulationAmplitude = baseAmplitude + (decimal)(random.NextDouble() - 0.5d) * 2m;
                var fieldAmplitude = simulationAmplitude + (decimal)(random.NextDouble() - 0.5d) * 8m;

                simulationSamples.Add(new AcousticSample
                {
                    Dataset = simulation,
                    Timestamp = timestamp,
                    FrequencyBand = band,
                    AmplitudeDb = decimal.Round(simulationAmplitude, 4),
                    DepthMeters = 60,
                    RangeMeters = 1000 + minute * 20,
                    SoundSpeed = 1498 + (decimal)Math.Sin(minute / 20d),
                    NoiseLevelDb = -90 + (decimal)(random.NextDouble() * 4)
                });
                fieldSamples.Add(new AcousticSample
                {
                    Dataset = field,
                    Timestamp = timestamp,
                    FrequencyBand = band,
                    AmplitudeDb = decimal.Round(fieldAmplitude, 4),
                    DepthMeters = 60 + random.Next(-2, 3),
                    RangeMeters = 1000 + minute * 20 + random.Next(-25, 26),
                    SoundSpeed = 1497 + (decimal)Math.Sin(minute / 17d),
                    NoiseLevelDb = -88 + (decimal)(random.NextDouble() * 5)
                });
            }
        }

        dbContext.AcousticSamples.AddRange(simulationSamples);
        dbContext.AcousticSamples.AddRange(fieldSamples);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedBundledArlutPartAFieldDatasetAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Datasets.AsNoTracking().AnyAsync(x => x.Name == BundledArlutPartAFieldDatasetName, cancellationToken))
        {
            return;
        }

        var csvPath = ResolveBundledArlutStride2500CsvPath(System.AppContext.BaseDirectory);
        if (!File.Exists(csvPath))
        {
            Console.Error.WriteLine($"[DatabaseInitializer] Bundled ARLUT CSV not found at {csvPath}; skip seed.");
            return;
        }

        var csv = await File.ReadAllTextAsync(csvPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(csv))
        {
            Console.Error.WriteLine($"[DatabaseInitializer] Bundled ARLUT CSV at {csvPath} is empty; skip seed.");
            return;
        }

        var dataset = new Dataset
        {
            Name = BundledArlutPartAFieldDatasetName,
            Type = "field",
            SourceSystem = "arlut-csv-bundled",
            Version = "partA-01-stride2500",
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Datasets.Add(dataset);
        await dbContext.SaveChangesAsync(cancellationToken);

        var imported = await AcousticCsvBatchImporter.ImportIntoDatasetAsync(dbContext, dataset, csv, cancellationToken);
        Console.WriteLine(
            $"[DatabaseInitializer] Seeded {BundledArlutPartAFieldDatasetName} with {imported} samples from {csvPath}.");
    }

    /// <summary>Published build: <c>seed-data/</c>; dev: repo <c>../../data/</c>.</summary>
    private static string ResolveBundledArlutStride2500CsvPath(string contentRoot)
    {
        var fromOutput = Path.GetFullPath(Path.Combine(contentRoot, "seed-data", "ARLUT_01_partA_01_dataset_field_stride2500.csv"));
        if (File.Exists(fromOutput))
        {
            return fromOutput;
        }

        return Path.GetFullPath(Path.Combine(contentRoot, "..", "..", "data", "ARLUT_01_partA_01_dataset_field_stride2500.csv"));
    }
}
