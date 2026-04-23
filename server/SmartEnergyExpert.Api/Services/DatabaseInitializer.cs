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
            await SeedCriteriaAsync(dbContext, cancellationToken);

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

    private static async Task SeedCriteriaAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        if (await dbContext.Criteria.AnyAsync(cancellationToken))
        {
            return;
        }

        var criteria = new[]
        {
            new Criterion
            {
                Name = "temperature",
                Description = "Observed equipment temperature",
                MinValue = 0,
                MaxValue = 120,
                DefaultWeight = 0.35m
            },
            new Criterion
            {
                Name = "vibration",
                Description = "Observed vibration level",
                MinValue = 0,
                MaxValue = 25,
                DefaultWeight = 0.35m
            },
            new Criterion
            {
                Name = "pressure",
                Description = "Observed pressure level",
                MinValue = 0,
                MaxValue = 16,
                DefaultWeight = 0.30m
            }
        };

        dbContext.Criteria.AddRange(criteria);
        await dbContext.SaveChangesAsync(cancellationToken);

        var weights = criteria.Select(x => new CriterionWeight
        {
            CriterionId = x.Id,
            ExperimentType = "default",
            Weight = x.DefaultWeight
        });

        dbContext.CriterionWeights.AddRange(weights);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
