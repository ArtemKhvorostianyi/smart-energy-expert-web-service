using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartEnergyExpert.Client.Data;

namespace SmartEnergyExpert.Client.Connections.AppDb;

public sealed class AppDbContextFactory(IConfiguration configuration) : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(ResolveConnectionString(configuration));
        return new AppDbContext(optionsBuilder.Options);
    }

    internal static string ResolveConnectionString(IConfiguration configuration)
    {
        var conn = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(conn))
        {
            return conn;
        }

        return "Host=localhost;Port=5432;Database=hydroacoustic_expert;Username=postgres;Password=postgres";
    }
}
