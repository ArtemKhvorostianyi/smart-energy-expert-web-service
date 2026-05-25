using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartEnergyExpert.Client.Data;
using SmartEnergyExpert.Client.Services;

namespace SmartEnergyExpert.Client.Connections.AppDb;

public sealed class AppDbConnection : IConnection, IHaveSecrets
{
    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .SetBasePath(System.AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
            .Build();

    public string GetContext(string connectionPath) => string.Empty;

    public string GetNamespace() => typeof(AppDbConnection).Namespace!;

    public string GetName() => "AppDb";

    public string GetConnectionType() => "Database";

    public ConnectionEntity[] GetEntities() =>
    [
        new("Dataset", "Datasets"),
        new("AcousticSample", "AcousticSamples"),
        new("ComparisonRun", "ComparisonRuns"),
        new("User", "Users")
    ];

    public void RegisterServices(Server server)
    {
        server.Services.AddSingleton(BuildConfiguration());
        server.Services.AddSingleton<AppDbContextFactory>();
        server.Services.AddSingleton<IDbContextFactory<AppDbContext>>(sp =>
            sp.GetRequiredService<AppDbContextFactory>());
        server.Services.AddScoped<IComparisonService, ComparisonService>();
        server.Services.AddScoped<IParameterSyntheticSimulationService, ParameterSyntheticSimulationService>();
        server.Services.AddScoped<IApiClient, HydroacousticService>();
    }

    public async Task<(bool ok, string? message)> TestConnection(IConfiguration config)
    {
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(AppDbContextFactory.ResolveConnectionString(config))
                .Options;
            await using var db = new AppDbContext(options);
            var canConnect = await db.Database.CanConnectAsync();
            return canConnect
                ? (true, null)
                : (false, "Cannot connect to PostgreSQL.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public Secret[] GetSecrets() => [new("ConnectionStrings:DefaultConnection")];
}
