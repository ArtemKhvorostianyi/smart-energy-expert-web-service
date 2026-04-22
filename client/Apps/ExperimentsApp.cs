namespace SmartEnergyExpert.Client.Apps;

[App(icon: Icons.FlaskConical, title: "Experiments", searchHints: ["experiments", "parameters", "input", "measurements"])]
public sealed class ExperimentsApp : ViewBase
{
    public override object? Build()
    {
        return Layout.Vertical().Padding(4).Gap(2)
               | Text.H2("Experiment Input Workspace")
               | Text.Markdown("This screen will contain UI for creating experiments, entering parameters, and importing CSV files.")
               | new Card(
                   Layout.Vertical().Gap(1)
                   | Text.H3("Screen Plan")
                   | Text.Block("- Experiment metadata")
                   | Text.Block("- Measurement parameters")
                   | Text.Block("- Status: draft / submitted / evaluated")
                   | Text.Block("- Input data validation")
               );
    }
}
