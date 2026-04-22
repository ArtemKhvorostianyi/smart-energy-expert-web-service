namespace SmartEnergyExpert.Client.Apps;

[App(icon: Icons.FlaskConical, title: "Experiments", searchHints: ["experiments", "parameters", "input", "measurements"])]
public sealed class ExperimentsApp : ViewBase
{
    public override object? Build()
    {
        return Layout.Vertical().Padding(4).Gap(2)
               | Text.H2("Experiment Input Workspace")
               | Text.Markdown("Тут буде UI для створення експерименту, вводу параметрів та імпорту CSV.")
               | new Card(
                   Layout.Vertical().Gap(1)
                   | Text.H3("План екрана")
                   | Text.Block("- Метадані експерименту")
                   | Text.Block("- Параметри вимірювань")
                   | Text.Block("- Статус: draft / submitted / evaluated")
                   | Text.Block("- Перевірка коректності вхідних даних")
               );
    }
}
