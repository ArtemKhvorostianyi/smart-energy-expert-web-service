namespace SmartEnergyExpert.Client.Apps;

[App(icon: Icons.ShieldAlert, title: "Evaluations", searchHints: ["evaluations", "risk", "recommendation", "score"])]
public sealed class EvaluationsApp : ViewBase
{
    public override object? Build()
    {
        return Layout.Vertical().Padding(4).Gap(2)
               | Text.H2("Evaluation Results")
               | Text.Markdown("Тут відображатимуться інтегральна оцінка, рівень ризику і рекомендація для рішення.")
               | new Card(
                   Layout.Vertical().Gap(1)
                   | Text.H3("Алгоритм")
                   | Text.Block("IntegralScore = Sum(ParameterScore * Weight) / Sum(Weight)")
                   | Text.Block("Risk mapping за діапазонами 0..1")
                   | Text.Block("Генерація рекомендації залежно від ризику")
               );
    }
}
