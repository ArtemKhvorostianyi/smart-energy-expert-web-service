using Ivy;
using SmartEnergyExpert.Client.Apps;
using SmartEnergyExpert.Client.Services;

var server = new Server();
server.UseCulture("en-US");
server.Services.AddSingleton<IApiClient, ApiClient>();
server.AddAppsFromAssembly();
server.AddConnectionsFromAssembly();
server.UseAppShell(new AppShellSettings().DefaultApp<DashboardApp>().UseTabs(preventDuplicates: true));
await server.RunAsync();
