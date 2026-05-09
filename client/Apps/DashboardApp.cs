namespace SmartEnergyExpert.Client.Apps;

[App(icon: Icons.LayoutDashboard, title: "Dashboard", searchHints: ["dashboard", "risk", "overview", "evaluation"])]
public sealed class DashboardApp : ViewBase
{
    public override object? Build()
    {
        return Layout.Vertical().Gap(2)
               | Text.H1("Hydroacoustic Comparison Service")
               | Text.P("Local Ivy application for comparing hydroacoustic simulation outputs against field measurements.")
               | new Separator()
               | Text.H3("Workflow")
               | Text.Block("1. Ensure API and PostgreSQL run on localhost.")
               | Text.Block("2. Open Datasets management for CSV/import and cleanup, or Environment simulation for a synthetic model dataset.")
               | Text.Block("3. Open Hydroacoustic Comparison.")
               | Text.Block("4. Select simulation and field datasets.")
               | Text.Block("5. Run compa=rison and inspect top differences with recommendations.");
    }
}
