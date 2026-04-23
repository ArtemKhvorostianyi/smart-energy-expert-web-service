namespace SmartEnergyExpert.Client.Apps;

[App(icon: Icons.ShieldAlert, title: "Evaluations", searchHints: ["evaluations", "risk", "recommendation", "score"])]
public sealed class EvaluationsApp : ViewBase
{
    public override object? Build()
    {
        var apiClient = UseService<SmartEnergyExpert.Client.Services.IApiClient>();
        var selectedExperimentId = UseState<Guid?>(null);
        var conclusion = UseState("");
        var statusMessage = UseState("");
        var latestResult = UseState<SmartEnergyExpert.Client.Services.EvaluationResultDto?>(null);
        var refreshTick = UseState(0);

        var experimentsQuery = UseQuery(
            key: (nameof(EvaluationsApp), refreshTick.Value),
            fetcher: async ct => await apiClient.GetExperimentsAsync(ct));
        var experiments = experimentsQuery.Value ?? [];

        return Layout.Vertical().Padding(4).Gap(2)
               | Text.H2("Evaluation Results")
               | new Card(
                   Layout.Vertical()
                   | Text.H3("Run Evaluation")
                   | Text.Block((selectedExperimentId.Value ?? experiments.FirstOrDefault()?.Id) is null
                       ? "Selected experiment: not selected"
                       : $"Selected experiment: {selectedExperimentId.Value ?? experiments.First().Id}")
                   | conclusion.ToTextInput().Placeholder("Expert conclusion (optional)")
                   | new Button("Evaluate Experiment")
                       .Primary()
                       .OnClick(async () =>
                       {
                           try
                           {
                               var experimentId = selectedExperimentId.Value ?? experiments.FirstOrDefault()?.Id;
                               if (experimentId is null)
                               {
                                   throw new InvalidOperationException("No experiment available to evaluate.");
                               }

                               var result = await apiClient.EvaluateAsync(experimentId.Value, conclusion.Value);
                               latestResult.Set(result);
                               statusMessage.Set("Evaluation completed.");
                               refreshTick.Set(refreshTick.Value + 1);
                           }
                           catch (Exception ex)
                           {
                               statusMessage.Set($"Evaluation failed: {ex.Message}");
                           }
                       }))
               | (string.IsNullOrWhiteSpace(statusMessage.Value) ? new Fragment() : Text.Block(statusMessage.Value))
               | (latestResult.Value is null
                   ? new Fragment()
                   : new Card(
                       Layout.Vertical()
                       | Text.H3("Latest Result")
                       | Text.Block($"Score: {latestResult.Value.IntegralScore:F4}")
                       | Text.Block($"Risk: {latestResult.Value.RiskLevel}")
                       | Text.Block($"Recommendation: {latestResult.Value.Recommendation}")))
               | Text.H3("History")
               | (experimentsQuery.Loading
                   ? Skeleton.Card()
                   : experimentsQuery.Error is { } err
                       ? Callout.Error($"Failed to load history: {err.Message}")
                       : new Card(
                           Layout.Vertical()
                           | new Button("Use latest experiment for evaluation")
                               .OnClick(() =>
                               {
                                   if (experiments.Count > 0)
                                   {
                                       selectedExperimentId.Set(experiments[0].Id);
                                   }
                               })
                           | string.Join(
                               Environment.NewLine,
                               experiments
                                   .Where(x => string.Equals(x.Status, "evaluated", StringComparison.OrdinalIgnoreCase))
                                   .Select(x => $"{x.Id} | {x.Title} | {x.ExperimentType} | {x.Status} | {x.CreatedAt:yyyy-MM-dd HH:mm}"))));
    }
}
