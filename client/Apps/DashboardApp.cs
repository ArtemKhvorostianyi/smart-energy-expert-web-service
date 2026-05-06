namespace SmartEnergyExpert.Client.Apps;

[App(icon: Icons.LayoutDashboard, title: "Dashboard", searchHints: ["dashboard", "risk", "overview", "evaluation"])]
public sealed class DashboardApp : ViewBase
{
    public override object? Build()
    {
        var navigator = UseNavigation();

        return Layout.Vertical().Gap(2)
               | Text.H1("Hydroacoustic Comparison Service")
               | Text.P("Local Ivy application for comparing hydroacoustic simulation outputs against field measurements.")
               | new Separator()
               | new Card(
                   Layout.Vertical()
                   | Text.H3("Authorization")
                   | Text.Block("Authentication is handled by Ivy Basic Auth.")
                   | Text.Block("If the session expires, Ivy will redirect to the authorization page automatically.")
                   | new Button("Logout")
                       .OnClick(() => navigator.Navigate("app://logout")))
               | new Separator()
               | Text.H3("Workflow")
               | Text.Block("1. Ensure API and PostgreSQL run on localhost.")
               | Text.Block("2. Open Hydroacoustic Comparison app.")
               | Text.Block("3. Select simulation and field datasets.")
               | Text.Block("4. Run comparison and inspect top differences with recommendations.");
    }
}
