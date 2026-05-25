using Microsoft.EntityFrameworkCore;
using SmartEnergyExpert.Client.Data;
using SmartEnergyExpert.Client.DTOs;
using SmartEnergyExpert.Client.Entities;
using SmartEnergyExpert.Client.Services.Auth;

namespace SmartEnergyExpert.Client.Services;

public sealed class DatabaseInitializer(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var dbContext = await dbFactory.CreateDbContextAsync(cancellationToken);

            await dbContext.Database.MigrateAsync(cancellationToken);
            await SeedRolesAsync(dbContext, cancellationToken);
            await RetireLegacyPublicDatasetsAsync(dbContext, cancellationToken);
            await SeedSharedArlutPairAsync(dbContext, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[DatabaseInitializer] Skipped: {ex.Message}. Ensure PostgreSQL is running and ConnectionStrings:DefaultConnection is set.");
        }
    }

    private static async Task SeedRolesAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        foreach (var roleName in new[] { AuthRoles.Analyst, AuthRoles.Guest })
        {
            if (!await dbContext.Roles.AnyAsync(x => x.Name == roleName, cancellationToken))
            {
                dbContext.Roles.Add(new Role { Name = roleName });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Прибирає старі публічні демо-датасети без власника (синтетика, старий bundled ARLUT).</summary>
    private static async Task RetireLegacyPublicDatasetsAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var legacy = await dbContext.Datasets
            .Where(x => !x.IsGuestCatalog && x.OwnerUserId == null
                        && (x.SourceSystem == "synthetic-generator"
                            || x.SourceSystem == "arlut-csv-bundled"
                            || x.SourceSystem == "guest-arlut"
                            || x.Name == "synthetic-simulation-v1"
                            || x.Name == "synthetic-field-v1"))
            .ToListAsync(cancellationToken);

        if (legacy.Count == 0)
        {
            return;
        }

        await RemoveDatasetsWithRunsAsync(dbContext, legacy, cancellationToken);
        Console.WriteLine($"[DatabaseInitializer] Removed {legacy.Count} legacy public dataset(s).");
    }

    private static async Task SeedSharedArlutPairAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var catalog = await dbContext.Datasets
            .Where(x => x.IsGuestCatalog && x.SourceSystem == SharedArlutCatalog.SourceSystem)
            .ToListAsync(cancellationToken);

        if (await IsSharedCatalogReadyAsync(dbContext, catalog, cancellationToken))
        {
            return;
        }

        if (catalog.Count > 0)
        {
            await RemoveDatasetsWithRunsAsync(dbContext, catalog, cancellationToken);
            Console.WriteLine($"[DatabaseInitializer] Replacing outdated shared ARLUT catalog ({catalog.Count} dataset(s)).");
        }

        var fieldCsvPath = ResolveSeedCsvPath(SharedArlutCatalog.FieldCsvFileName);
        if (!File.Exists(fieldCsvPath))
        {
            Console.Error.WriteLine(
                $"[DatabaseInitializer] Shared ARLUT field CSV missing at {fieldCsvPath}; skip catalog seed.");
            return;
        }

        var fieldCsv = await File.ReadAllTextAsync(fieldCsvPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(fieldCsv))
        {
            Console.Error.WriteLine("[DatabaseInitializer] Shared ARLUT field CSV empty; skip catalog seed.");
            return;
        }

        var field = new Dataset
        {
            Name = SharedArlutCatalog.FieldName,
            Type = "field",
            SourceSystem = SharedArlutCatalog.SourceSystem,
            Version = SharedArlutCatalog.CatalogVersion,
            IsGuestCatalog = true,
            OwnerUserId = null
        };

        dbContext.Datasets.Add(field);
        await dbContext.SaveChangesAsync(cancellationToken);

        var fieldImported = await AcousticCsvBatchImporter.ImportIntoDatasetAsync(
            dbContext, field, fieldCsv, cancellationToken);

        var simulationService = new ParameterSyntheticSimulationService();
        var (_, simImported) = await simulationService.GenerateAndPersistAsync(
            dbContext,
            new GenerateSimulationDatasetRequest
            {
                Name = SharedArlutCatalog.SimulationName,
                AlignToFieldDatasetId = field.Id,
                DepthMeters = 60,
                TemperatureCelsius = 12,
                SalinityPsu = 35,
                NoiseLevelDb = -92,
                BottomType = "sand",
                DurationMinutes = 60,
                ModelVersion = "arlut-shared-field-aligned"
            },
            ownerUserId: null,
            isSharedCatalog: true,
            cancellationToken);

        Console.WriteLine(
            $"[DatabaseInitializer] Shared ARLUT pair: field {fieldImported} samples, simulation {simImported} samples.");
    }

    private static async Task<bool> IsSharedCatalogReadyAsync(
        AppDbContext dbContext,
        List<Dataset> catalog,
        CancellationToken cancellationToken)
    {
        if (catalog.Count != 2)
        {
            return false;
        }

        if (catalog.Any(x => x.Version != SharedArlutCatalog.CatalogVersion))
        {
            return false;
        }

        var sim = catalog.FirstOrDefault(x => x.Type == "simulation");
        var field = catalog.FirstOrDefault(x => x.Type == "field");
        if (sim is null || field is null)
        {
            return false;
        }

        var simCount = await dbContext.AcousticSamples.CountAsync(x => x.DatasetId == sim.Id, cancellationToken);
        var fieldCount = await dbContext.AcousticSamples.CountAsync(x => x.DatasetId == field.Id, cancellationToken);
        return simCount >= SharedArlutCatalog.MinExpectedSamples
               && fieldCount >= SharedArlutCatalog.MinExpectedSamples;
    }

    private static async Task RemoveDatasetsWithRunsAsync(
        AppDbContext dbContext,
        List<Dataset> datasets,
        CancellationToken cancellationToken)
    {
        foreach (var dataset in datasets)
        {
            var linkedRuns = await dbContext.ComparisonRuns
                .Where(r => r.SimulationDatasetId == dataset.Id || r.FieldDatasetId == dataset.Id)
                .ToListAsync(cancellationToken);
            dbContext.ComparisonRuns.RemoveRange(linkedRuns);
        }

        dbContext.Datasets.RemoveRange(datasets);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string ResolveSeedCsvPath(string fileName)
    {
        var fromOutput = Path.GetFullPath(Path.Combine(System.AppContext.BaseDirectory, "seed-data", fileName));
        if (File.Exists(fromOutput))
        {
            return fromOutput;
        }

        return Path.GetFullPath(Path.Combine(System.AppContext.BaseDirectory, "..", "..", "data", fileName));
    }
}
