using Microsoft.EntityFrameworkCore;
using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.Entities;

namespace SmartEnergyExpert.Api.Services;

public sealed class DatabaseInitializer(IServiceProvider serviceProvider, ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await dbContext.Database.MigrateAsync(cancellationToken);
            await SeedRolesAndUsersAsync(dbContext, cancellationToken);
            await SeedSyntheticDatasetsAsync(dbContext, cancellationToken);

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
}
