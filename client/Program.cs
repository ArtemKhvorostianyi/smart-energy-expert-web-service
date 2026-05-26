using System.Reflection;
using Ivy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using QuestPDF.Infrastructure;
using SmartEnergyExpert.Client.Apps;
using SmartEnergyExpert.Client.Connections.AppDb;
using SmartEnergyExpert.Client.Services;
using SmartEnergyExpert.Client.Services.Auth;

QuestPDF.Settings.License = LicenseType.Community;

// macOS AirPlay Receiver займає *:5000 → HTTP 403. За замовчуванням 5010; поважаємо PORT / ivy run --port.
static int ResolveDevPort()
{
    if (int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var fromEnv) && fromEnv is > 0 and not 5000)
        return fromEnv;
    return 5010;
}

var devPort = ResolveDevPort();
var listenUrl = $"http://127.0.0.1:{devPort}";
Environment.SetEnvironmentVariable("PORT", devPort.ToString());
Environment.SetEnvironmentVariable("ASPNETCORE_URLS", listenUrl);

var server = new Server(new ServerArgs { Port = devPort });
server.UseWebApplicationBuilder(builder => builder.WebHost.UseUrls(listenUrl));
server.UseCulture("uk-UA");
server.AddConnectionsFromAssembly();
server.AddAppsFromAssembly();
server.UseAuth<AppBasicAuthProvider>(viewFactory: () => new AuthApp());
server.UseAppShell(new AppShellSettings().DefaultApp<DashboardApp>().UseTabs(preventDuplicates: true));

var configuration = new ConfigurationBuilder()
    .SetBasePath(System.AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
    .Build();
var dbFactory = new AppDbContextFactory(configuration);
await new DatabaseInitializer(dbFactory).InitializeAsync(CancellationToken.None);

Console.WriteLine($"Open the app at {listenUrl}");
await server.RunAsync();
