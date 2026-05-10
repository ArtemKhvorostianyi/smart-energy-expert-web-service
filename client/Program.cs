using Ivy;
using QuestPDF.Infrastructure;
using SmartEnergyExpert.Client.Apps;
using SmartEnergyExpert.Client.Services;

QuestPDF.Settings.License = LicenseType.Community;

var server = new Server();
server.UseCulture("uk-UA");
server.Services.AddSingleton<IApiClient, ApiClient>();
server.AddAppsFromAssembly();
server.UseAppShell(new AppShellSettings().DefaultApp<DashboardApp>().UseTabs(preventDuplicates: true));
await server.RunAsync();
