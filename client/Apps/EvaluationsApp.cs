namespace SmartEnergyExpert.Client.Apps;

[App(icon: Icons.ShieldAlert, title: "Evaluations", searchHints: ["evaluations", "risk", "recommendation", "score"])]
public sealed class EvaluationsApp : ViewBase
{
    public override object? Build()
    {
        return Layout.Vertical().Padding(4).Gap(2)
               | Text.H2("Evaluation Results")
               | Text.Markdown("This screen will show the integral score, risk level, and the generated decision recommendation.")
               | new Card(
                   Layout.Vertical().Gap(1)
                   | Text.H3("Algorithm")
                   | Text.Block("IntegralScore = Sum(ParameterScore * Weight) / Sum(Weight)")
                   | Text.Block("Risk mapping by score intervals 0..1")
                   | Text.Block("Recommendation generation based on risk level")
               );
    }
}
