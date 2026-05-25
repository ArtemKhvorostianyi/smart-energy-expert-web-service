using System.Reflection;
using Ivy;
using Microsoft.Extensions.Configuration;
using QuestPDF.Infrastructure;
using SmartEnergyExpert.Client.Apps;
using SmartEnergyExpert.Client.Connections.AppDb;
using SmartEnergyExpert.Client.Services;

QuestPDF.Settings.License = LicenseType.Community;

var server = new Server();
server.UseCulture("uk-UA");
server.AddConnectionsFromAssembly();
server.AddAppsFromAssembly();
server.UseAppShell(new AppShellSettings().DefaultApp<DashboardApp>().UseTabs(preventDuplicates: true));

var configuration = new ConfigurationBuilder()
    .SetBasePath(System.AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
    .Build();
var dbFactory = new AppDbContextFactory(configuration);
await new DatabaseInitializer(dbFactory).InitializeAsync(CancellationToken.None);

await server.RunAsync();
