namespace SmartEnergyExpert.Client.Apps;

[App(icon: Icons.ShieldAlert, title: "Evaluations", searchHints: ["evaluations", "risk", "recommendation", "score"])]
public sealed class EvaluationsApp : ViewBase
{
    public override object? Build()
    {
        var apiClient = UseService<SmartEnergyExpert.Client.Services.IApiClient>();
        var selectedExperimentKey = UseState("");
        var statusMessage = UseState("");
        var latestResult = UseState<SmartEnergyExpert.Client.Services.EvaluationResultDto?>(null);
        var refreshTick = UseState(0);

        var experimentsQuery = UseQuery(
            key: (nameof(EvaluationsApp), refreshTick.Value),
            fetcher: async ct => await apiClient.GetExperimentsAsync(ct));
        var experiments = experimentsQuery.Value ?? [];
        var experimentOptions = experiments
            .Select(x => BuildExperimentOption(x.Id, x.Title, x.ExperimentType, x.CreatedAt))
            .ToArray();
        var activeExperimentKey = string.IsNullOrWhiteSpace(selectedExperimentKey.Value) || !experimentOptions.Contains(selectedExperimentKey.Value)
            ? experimentOptions.FirstOrDefault() ?? string.Empty
            : selectedExperimentKey.Value;
        object experimentSelector = experimentsQuery.Loading
            ? Skeleton.Card()
            : experimentOptions.Length == 0
                ? Callout.Warning("No experiments found. Create an experiment first.")
                : selectedExperimentKey.ToSelectInput(experimentOptions);

        return Layout.Vertical().Padding(4).Gap(2)
               | Text.H2("Evaluation Results")
               | new Card(
                   Layout.Vertical()
                   | Text.H3("Select Experiment")
                   | experimentSelector
                   | Text.Muted("Choose experiment by name and run evaluation.")
                   | Text.H3("Run Evaluation")
                   | new Button("Evaluate Experiment")
                       .Primary()
                       .OnClick(async () =>
                       {
                           try
                           {
                               var selectedExperiment = experiments.FirstOrDefault(x =>
                                   BuildExperimentOption(x.Id, x.Title, x.ExperimentType, x.CreatedAt) == activeExperimentKey);
                               if (selectedExperiment is null)
                               {
                                   throw new InvalidOperationException("Select an experiment first.");
                               }

                               var result = await apiClient.EvaluateAsync(selectedExperiment.Id, string.Empty);
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
                       | Text.Block($"Status: {latestResult.Value.Status}")
                       | Text.Block($"Recommendation: {latestResult.Value.Recommendation}")))
               | (latestResult.Value is null || string.IsNullOrWhiteSpace(latestResult.Value.Explanation)
                   ? new Fragment()
                   : new Card(
                       Layout.Vertical()
                       | Text.H3("Explanation")
                       | Text.Block(latestResult.Value.Explanation)
                       | Text.Block($"Top factors: {(latestResult.Value.TopFactors.Length == 0 ? "none" : string.Join(", ", latestResult.Value.TopFactors))}")));
    }

    private static string BuildExperimentOption(Guid id, string title, string experimentType, DateTimeOffset createdAt) =>
        $"{title} ({experimentType}, {createdAt:yyyy-MM-dd HH:mm}) [{id.ToString()[..8]}]";

}
