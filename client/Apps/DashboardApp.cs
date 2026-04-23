namespace SmartEnergyExpert.Client.Apps;

[App(icon: Icons.LayoutDashboard, title: "Dashboard", searchHints: ["dashboard", "risk", "overview", "evaluation"])]
public sealed class DashboardApp : ViewBase
{
    public override object? Build()
    {
        var navigator = UseNavigation();

        return Layout.Vertical().Padding(4).Gap(2)
               | Text.H1("Smart Energy Expert")
               | Text.P("Web service for expert evaluation to support decision-making in energy cyber-physical systems.")
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
               | Text.Block("1. Login in Dashboard")
               | Text.Block("2. Create experiment and add parameter in Experiments")
               | Text.Block("3. Run evaluation in Evaluations")
               | Text.Block("4. Review history in Evaluations");
    }
}
