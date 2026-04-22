namespace SmartEnergyExpert.Client.Apps;

[App(icon: Icons.LayoutDashboard, title: "Dashboard", searchHints: ["dashboard", "risk", "overview", "evaluation"])]
public sealed class DashboardApp : ViewBase
{
    public override object? Build()
    {
        return Layout.Vertical().Padding(4).Gap(2)
               | Text.H1("Smart Energy Expert")
               | Text.Markdown("Web service for expert evaluation to support decision-making in energy cyber-physical systems.")
               | new Separator()
               | Layout.Horizontal().Gap(2)
                   | new Card(
                       Layout.Vertical().Gap(1)
                       | Text.H3("MVP Modules")
                       | Text.Block("1. Authentication and roles")
                       | Text.Block("2. Experiments and parameters")
                       | Text.Block("3. Expert evaluation")
                       | Text.Block("4. Recommendations and reports")
                   )
                   | new Card(
                       Layout.Vertical().Gap(1)
                       | Text.H3("Risk Levels")
                       | Text.Block("0.00 - 0.25 : Low")
                       | Text.Block("0.26 - 0.50 : Moderate")
                       | Text.Block("0.51 - 0.75 : High")
                       | Text.Block("0.76 - 1.00 : Critical")
                   )
               | new Separator()
               | Text.Markdown("To get started, open the **Experiments** and **Evaluations** apps in App Shell tabs.");
    }
}
