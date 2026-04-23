using Ivy;
using SmartEnergyExpert.Client.Apps;

var server = new Server();
server.UseCulture("en-US");
server.AddAppsFromAssembly();
server.AddConnectionsFromAssembly();
server.UseAppShell(new AppShellSettings().DefaultApp<DashboardApp>().UseTabs(preventDuplicates: true));
await server.RunAsync();
