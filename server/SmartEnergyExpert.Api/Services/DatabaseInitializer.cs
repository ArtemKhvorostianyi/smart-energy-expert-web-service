using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.Entities;

namespace SmartEnergyExpert.Api.Services;

public sealed class DatabaseInitializer(IServiceProvider serviceProvider, ILogger<DatabaseInitializer> logger)
{
    internal const string BundledArlutPartAFieldDatasetName = "ARLUT 01 part A field stride2500";
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await dbContext.Database.MigrateAsync(cancellationToken);
            await SeedRolesAndUsersAsync(dbContext, cancellationToken);
            await SeedSyntheticDatasetsAsync(dbContext, cancellationToken);

            var hostEnvironment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
            await SeedBundledArlutPartAFieldDatasetAsync(dbContext, hostEnvironment, logger, cancellationToken);

            logger.LogInformation("Database initialization completed.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database initialization skipped. Ensure PostgreSQL is running and connection string is correct.");
        }
    }

    private static async Task SeedRolesAndUsersAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!await dbContext.Roles.AnyAsync(cancellationToken))
        {
            var adminRole = new Role { Name = "Admin" };
            var expertRole = new Role { Name = "Expert" };
            var operatorRole = new Role { Name = "Operator" };

            dbContext.Roles.AddRange(adminRole, expertRole, operatorRole);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!await dbContext.Users.AnyAsync(cancellationToken))
        {
            var roles = await dbContext.Roles.ToDictionaryAsync(x => x.Name, cancellationToken);

            dbContext.Users.AddRange(
                new User
                {
                    FullName = "System Admin",
                    Email = "admin@smartenergy.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                    RoleId = roles["Admin"].Id
                },
                new User
                {
                    FullName = "Lead Expert",
                    Email = "expert@smartenergy.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Expert123!"),
                    RoleId = roles["Expert"].Id
                },
                new User
                {
                    FullName = "Field Operator",
                    Email = "operator@smartenergy.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Operator123!"),
                    RoleId = roles["Operator"].Id
                }
            );

            await dbContext.SaveChangesAsync(cancellationToken);
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
        IHostEnvironment hostEnvironment,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Datasets.AsNoTracking().AnyAsync(x => x.Name == BundledArlutPartAFieldDatasetName, cancellationToken))
        {
            return;
        }

        var csvPath = ResolveBundledArlutStride2500CsvPath(hostEnvironment.ContentRootPath);
        if (!File.Exists(csvPath))
        {
            logger.LogWarning("Bundled ARLUT CSV not found at {Path}; skip seed.", csvPath);
            return;
        }

        var csv = await File.ReadAllTextAsync(csvPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(csv))
        {
            logger.LogWarning("Bundled ARLUT CSV at {Path} is empty; skip seed.", csvPath);
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
        logger.LogInformation(
            "Seeded bundled field dataset {Name} with {Count} samples from {Path}.",
            BundledArlutPartAFieldDatasetName,
            imported,
            csvPath);
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
