using ClientServices = SmartEnergyExpert.Client.Services;

namespace SmartEnergyExpert.Client.Apps;

[App(icon: Icons.Waves, title: "Hydroacoustic Comparison", searchHints: ["hydroacoustic", "comparison", "charts", "blades", "preset"])]
public sealed class EvaluationsApp : ViewBase
{
    public override object? Build() => UseBlades(() => new WorkspaceBlade(), "Hydroacoustic Comparison");

    private sealed record ComparisonPreset(string Name, Guid SimulationDatasetId, Guid FieldDatasetId, int TopN);

    private sealed class WorkspaceBlade : ViewBase
    {
        public override object? Build()
        {
            var apiClient = UseService<ClientServices.IApiClient>();
            var blades = UseContext<IBladeContext>();
            var refreshTick = UseState(0);
            var selectedSimulation = UseState("");
            var selectedField = UseState("");
            var topN = UseState(15m);
            var status = UseState("");
            var result = UseState<ClientServices.ComparisonResultDto?>(null);
            var presetName = UseState("");
            var selectedPreset = UseState("");
            var presets = UseState(new List<ComparisonPreset>());

            var datasetsQuery = UseQuery(
                key: (nameof(WorkspaceBlade), refreshTick.Value),
                fetcher: async ct => await apiClient.GetDatasetsAsync(ct));
            var datasets = datasetsQuery.Value ?? [];
            var simOptions = datasets.Where(x => x.Type == "simulation").Select(ToOption).ToArray();
            var fieldOptions = datasets.Where(x => x.Type == "field").Select(ToOption).ToArray();
            var canRun = !datasetsQuery.Loading
                         && simOptions.Length > 0
                         && fieldOptions.Length > 0
                         && !string.IsNullOrWhiteSpace(selectedSimulation.Value)
                         && !string.IsNullOrWhiteSpace(selectedField.Value);

            return new Fragment()
                   | new BladeHeader(
                       Layout.Horizontal().Gap(2)
                       | new Button("Open Charts")
                           .OnClick(() =>
                           {
                               if (result.Value is null)
                               {
                                   status.Set("Run comparison first.");
                                   return;
                               }

                               blades.Push(this, new ChartsBlade(result.Value), "Charts", width: Size.Units(120));
                           }))
                   | Layout.Vertical().Gap(2)
                       | Text.H2("Hydroacoustic Comparison")
                       | BuildPresetCard(presetName, selectedPreset, presets, selectedSimulation, selectedField, topN, datasets, status)
                       | new Card(
                           Layout.Vertical()
                           | Text.H3("Selection")
                           | (datasetsQuery.Loading ? Skeleton.Card() : selectedSimulation.ToSelectInput(simOptions))
                           | (datasetsQuery.Loading ? Skeleton.Card() : selectedField.ToSelectInput(fieldOptions))
                           | topN.ToNumberInput(min: 5, max: 100).Placeholder("Top-N")
                           | new Button("Run Comparison").Primary().Disabled(!canRun).OnClick(async () =>
                           {
                               try
                               {
                                   if (!canRun)
                                   {
                                       status.Set("Select both datasets.");
                                       return;
                                   }

                                   var latest = await apiClient.RunComparisonAsync(new ClientServices.CreateComparisonRequestDto
                                   {
                                       SimulationDatasetId = ParseDatasetId(selectedSimulation.Value),
                                       FieldDatasetId = ParseDatasetId(selectedField.Value),
                                       TopN = (int)decimal.Clamp(topN.Value, 5, 100)
                                   });
                                   result.Set(latest);
                                   status.Set("Comparison completed.");
                               }
                               catch (Exception ex)
                               {
                                   status.Set($"Comparison failed: {ex.Message}");
                               }
                           }))
                       | (datasetsQuery.Error is { } e ? Callout.Warning(e.Message) : new Fragment())
                       | (string.IsNullOrWhiteSpace(status.Value) ? new Fragment() : Callout.Info(status.Value))
                       | (result.Value is null ? new Fragment() : BuildResultCards(result.Value));
        }

        private static object BuildPresetCard(
            IState<string> presetName,
            IState<string> selectedPreset,
            IState<List<ComparisonPreset>> presets,
            IState<string> selectedSimulation,
            IState<string> selectedField,
            IState<decimal> topN,
            IReadOnlyList<ClientServices.DatasetDto> datasets,
            IState<string> status)
        {
            var presetOptions = presets.Value.Select(x => x.Name).ToArray();
            return new Card(
                Layout.Vertical()
                | Text.H3("Presets")
                | presetName.ToTextInput().Placeholder("Preset name")
                | (presetOptions.Length == 0 ? Text.Muted("No presets.") : selectedPreset.ToSelectInput(presetOptions))
                | (Layout.Horizontal().Gap(2)
                    | new Button("Save").OnClick(() =>
                    {
                        var simId = TryParseDatasetId(selectedSimulation.Value);
                        var fieldId = TryParseDatasetId(selectedField.Value);
                        if (simId == Guid.Empty || fieldId == Guid.Empty)
                        {
                            status.Set("Select datasets before saving preset.");
                            return;
                        }

                        var name = string.IsNullOrWhiteSpace(presetName.Value) ? $"Preset {DateTimeOffset.UtcNow:HH:mm:ss}" : presetName.Value.Trim();
                        var next = presets.Value.ToList();
                        next.Add(new ComparisonPreset(name, simId, fieldId, (int)topN.Value));
                        presets.Set(next);
                        selectedPreset.Set(name);
                        status.Set("Preset saved.");
                    })
                    | new Button("Apply").Disabled(string.IsNullOrWhiteSpace(selectedPreset.Value)).OnClick(() =>
                    {
                        var preset = presets.Value.FirstOrDefault(x => x.Name == selectedPreset.Value);
                        if (preset is null)
                        {
                            status.Set("Preset not found.");
                            return;
                        }

                        var sim = datasets.FirstOrDefault(x => x.Id == preset.SimulationDatasetId);
                        var field = datasets.FirstOrDefault(x => x.Id == preset.FieldDatasetId);
                        if (sim is null || field is null)
                        {
                            status.Set("Preset datasets missing.");
                            return;
                        }

                        selectedSimulation.Set(ToOption(sim));
                        selectedField.Set(ToOption(field));
                        topN.Set(preset.TopN);
                        status.Set("Preset applied.");
                    })
                    | new Button("Delete").Disabled(string.IsNullOrWhiteSpace(selectedPreset.Value)).OnClick(() =>
                    {
                        var next = presets.Value.Where(x => x.Name != selectedPreset.Value).ToList();
                        presets.Set(next);
                        selectedPreset.Set("");
                        status.Set("Preset deleted.");
                    })));
        }

        private static object BuildResultCards(ClientServices.ComparisonResultDto result)
        {
            return Layout.Vertical().Gap(2)
                   | new Card(
                       Layout.Vertical()
                       | Text.H3("Quick Summary")
                       | Text.Block(result.SignificantDifferenceCount == 0
                           ? "Model matches field data well for this run."
                           : "Model needs tuning for part of the compared points."))
                   | new Card(
                       Layout.Vertical()
                       | Text.H3("Metrics")
                       | Text.Block($"MAE: {result.Mae:F3}")
                       | Text.Block($"RMSE: {result.Rmse:F3}")
                       | Text.Block($"MRE: {result.MeanRelativeErrorPercent:F2}%")
                       | Text.Block($"P95: {result.P95AbsoluteError:F3}")
                       | Text.Block($"Significant points: {result.SignificantDifferenceCount}/{result.TotalComparedPoints}"))
                   | new Card(
                       Layout.Vertical()
                       | Text.H3("Top Differences")
                       | new List(result.TopDifferences.Take(12).Select(x =>
                           new ListItem($"{x.Timestamp:HH:mm:ss.fff} | {x.FrequencyBand}Hz | rel={x.RelativeErrorPercent:F1}% | {x.Severity.ToUpperInvariant()}"))))
                   | new Card(
                       Layout.Vertical()
                       | Text.H3("Recommendations")
                       | new List(result.Recommendations.Select(x =>
                           new ListItem($"{x.ReasonCode} ({x.Confidence:P0}) — {x.SuggestedAction}"))));
        }
    }

    private sealed class ChartsBlade(ClientServices.ComparisonResultDto result) : ViewBase
    {
        public override object? Build()
        {
            var metricRows = new[]
            {
                new { Metric = "MAE", Value = (double)result.Mae },
                new { Metric = "RMSE", Value = (double)result.Rmse },
                new { Metric = "MRE", Value = (double)result.MeanRelativeErrorPercent },
                new { Metric = "P95", Value = (double)result.P95AbsoluteError }
            };

            var trendRows = result.TopDifferences
                .Take(20)
                .Select(x => new { Time = x.Timestamp.ToString("HH:mm:ss.fff"), Value = (double)x.RelativeErrorPercent })
                .ToArray();

            return Layout.Vertical().Gap(2)
                   | Text.H3("Charts")
                   | new Card(
                       Layout.Vertical()
                       | Text.Block("Metric comparison")
                       | metricRows.ToBarChart(
                           e => e.Metric,
                           [e => e.Sum(v => v.Value)],
                           BarChartStyles.Default))
                   | new Card(
                       Layout.Vertical()
                       | Text.Block("Top difference trend")
                       | trendRows.ToLineChart(
                           e => e.Time,
                           [e => e.Sum(v => v.Value)],
                           LineChartStyles.Dashboard));
        }
    }

    private static string ToOption(ClientServices.DatasetDto dataset) =>
        $"{dataset.Name} | {dataset.SourceSystem} | {dataset.SampleCount} samples [{dataset.Id}]";

    private static Guid TryParseDatasetId(string value)
    {
        try
        {
            return ParseDatasetId(value);
        }
        catch
        {
            return Guid.Empty;
        }
    }

    private static Guid ParseDatasetId(string value)
    {
        var openIndex = value.LastIndexOf('[');
        var closeIndex = value.LastIndexOf(']');
        if (openIndex < 0 || closeIndex <= openIndex)
        {
            throw new InvalidOperationException("Invalid dataset value.");
        }

        return Guid.Parse(value.Substring(openIndex + 1, closeIndex - openIndex - 1));
    }
}
