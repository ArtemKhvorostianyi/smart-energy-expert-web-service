namespace SmartEnergyExpert.Client.Apps;

[App(icon: Icons.Waves, title: "Hydroacoustic Comparison", searchHints: ["hydroacoustic", "comparison", "simulation", "field"])]
public sealed class EvaluationsApp : ViewBase
{
    public override object? Build()
    {
        var apiClient = UseService<SmartEnergyExpert.Client.Services.IApiClient>();
        var selectedSimulation = UseState("");
        var selectedField = UseState("");
        var statusMessage = UseState("");
        var latestResult = UseState<SmartEnergyExpert.Client.Services.ComparisonResultDto?>(null);

        var datasetsQuery = UseQuery(key: nameof(EvaluationsApp), fetcher: async ct => await apiClient.GetDatasetsAsync(ct));
        var datasets = datasetsQuery.Value ?? [];
        var simulationOptions = datasets.Where(x => x.Type == "simulation").Select(ToOption).ToArray();
        var fieldOptions = datasets.Where(x => x.Type == "field").Select(ToOption).ToArray();

        return Layout.Vertical().Gap(2)
               | Text.H2("Hydroacoustic Model vs Field Comparison")
               | new Card(
                   Layout.Vertical()
                   | Text.H3("Dataset Selection")
                   | (datasetsQuery.Loading ? Skeleton.Card() : selectedSimulation.ToSelectInput(simulationOptions))
                   | (datasetsQuery.Loading ? Skeleton.Card() : selectedField.ToSelectInput(fieldOptions))
                   | new Button("Run Comparison")
                       .Primary()
                       .OnClick(async () =>
                       {
                           try
                           {
                               var result = await apiClient.RunComparisonAsync(new SmartEnergyExpert.Client.Services.CreateComparisonRequestDto
                               {
                                   SimulationDatasetId = ParseDatasetId(selectedSimulation.Value),
                                   FieldDatasetId = ParseDatasetId(selectedField.Value),
                                   TopN = 15
                               });
                               latestResult.Set(result);
                               statusMessage.Set("Comparison completed.");
                           }
                           catch (Exception ex)
                           {
                               statusMessage.Set($"Failed to run comparison: {ex.Message}");
                           }
                       }))
               | (string.IsNullOrWhiteSpace(statusMessage.Value) ? new Fragment() : Callout.Info(statusMessage.Value))
               | (latestResult.Value is null
                   ? new Fragment()
                   : new Card(
                       Layout.Vertical()
                       | Text.H3("Metrics")
                       | Text.Block($"MAE: {latestResult.Value.Mae:F3}")
                       | Text.Block($"RMSE: {latestResult.Value.Rmse:F3}")
                       | Text.Block($"MRE: {latestResult.Value.MeanRelativeErrorPercent:F2}%")
                       | Text.Block($"P95 |error|: {latestResult.Value.P95AbsoluteError:F3}")
                       | Text.Block($"Significant points: {latestResult.Value.SignificantDifferenceCount}/{latestResult.Value.TotalComparedPoints}")))
               | (latestResult.Value is null
                   ? new Fragment()
                   : new Card(
                       Layout.Vertical()
                       | Text.H3("Top Differences")
                       | new List(latestResult.Value.TopDifferences.Select(x =>
                           new ListItem(
                               $"{x.Timestamp:HH:mm} | {x.FrequencyBand} Hz | rel={x.RelativeErrorPercent:F1}% | {x.Severity}. {x.Explanation}")))))
               | (latestResult.Value is null
                   ? new Fragment()
                   : new Card(
                       Layout.Vertical()
                       | Text.H3("Recommendations")
                       | new List(latestResult.Value.Recommendations.Select(x =>
                           new ListItem($"{x.ReasonCode} ({x.Confidence:P0}). {x.SuggestedAction}")))));
    }

    private static string ToOption(SmartEnergyExpert.Client.Services.DatasetDto dataset) =>
        $"{dataset.Name} [{dataset.Id}]";

    private static Guid ParseDatasetId(string value)
    {
        var openIndex = value.LastIndexOf('[');
        var closeIndex = value.LastIndexOf(']');
        return Guid.Parse(value.Substring(openIndex + 1, closeIndex - openIndex - 1));
    }
}
