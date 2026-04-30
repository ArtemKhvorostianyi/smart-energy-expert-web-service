namespace SmartEnergyExpert.Client.Apps;

[App(icon: Icons.Waves, title: "Hydroacoustic Comparison", searchHints: ["hydroacoustic", "comparison", "simulation", "field"])]
public sealed class EvaluationsApp : ViewBase
{
    public override object? Build()
    {
        var apiClient = UseService<SmartEnergyExpert.Client.Services.IApiClient>();
        var refreshTick = UseState(0);
        var selectedSimulation = UseState("");
        var selectedField = UseState("");
        var statusMessage = UseState("");
        var latestResult = UseState<SmartEnergyExpert.Client.Services.ComparisonResultDto?>(null);
        var newDatasetName = UseState("");
        var newDatasetType = UseState("simulation");
        var newDatasetSource = UseState("manual");
        var newDatasetVersion = UseState("v1");
        var importDataset = UseState("");

        var datasetsQuery = UseQuery(
            key: (nameof(EvaluationsApp), refreshTick.Value),
            fetcher: async ct => await apiClient.GetDatasetsAsync(ct));
        var datasets = datasetsQuery.Value ?? [];
        var simulationOptions = datasets.Where(x => x.Type == "simulation").Select(ToOption).ToArray();
        var fieldOptions = datasets.Where(x => x.Type == "field").Select(ToOption).ToArray();
        var allDatasetOptions = datasets.Select(ToOption).ToArray();
        var canRunComparison =
            !datasetsQuery.Loading &&
            simulationOptions.Length > 0 &&
            fieldOptions.Length > 0 &&
            !string.IsNullOrWhiteSpace(selectedSimulation.Value) &&
            !string.IsNullOrWhiteSpace(selectedField.Value);

        return Layout.Vertical().Gap(2)
               | Text.H2("Hydroacoustic Model vs Field Comparison")
               | Text.Muted("Compare modeled and field hydroacoustic signals, then review metric interpretation and actionable recommendations.")
               | new Card(
                   Layout.Vertical()
                   | Text.H3("Data Management")
                   | Text.Muted("Create new dataset or import sample CSV rows into an existing dataset.")
                   | newDatasetName.ToTextInput().Placeholder("Dataset name")
                   | newDatasetType.ToSelectInput(["simulation", "field"])
                   | newDatasetSource.ToTextInput().Placeholder("Source system, e.g. swellex, ut-austin, manual")
                   | newDatasetVersion.ToTextInput().Placeholder("Version, e.g. v1")
                   | new Button("Create Dataset")
                       .OnClick(async () =>
                       {
                           try
                           {
                               if (string.IsNullOrWhiteSpace(newDatasetName.Value))
                               {
                                   statusMessage.Set("Dataset name is required.");
                                   return;
                               }

                               await apiClient.CreateDatasetAsync(new SmartEnergyExpert.Client.Services.CreateDatasetRequestDto
                               {
                                   Name = newDatasetName.Value.Trim(),
                                   Type = newDatasetType.Value,
                                   SourceSystem = newDatasetSource.Value.Trim(),
                                   Version = newDatasetVersion.Value.Trim()
                               });
                               newDatasetName.Set("");
                               statusMessage.Set("Dataset created.");
                               refreshTick.Set(refreshTick.Value + 1);
                           }
                           catch (Exception ex)
                           {
                               statusMessage.Set($"Create dataset failed: {ex.Message}");
                           }
                       })
                   | (allDatasetOptions.Length == 0
                       ? Text.Muted("No datasets yet for CSV import.")
                       : importDataset.ToSelectInput(allDatasetOptions))
                   | new Button("Import Sample CSV into Selected Dataset")
                       .OnClick(async () =>
                       {
                           try
                           {
                               if (string.IsNullOrWhiteSpace(importDataset.Value))
                               {
                                   statusMessage.Set("Select a dataset for CSV import.");
                                   return;
                               }

                               var datasetId = ParseDatasetId(importDataset.Value);
                               var imported = await apiClient.ImportCsvSamplesAsync(datasetId, BuildSampleCsv());
                               statusMessage.Set($"CSV import completed. Imported rows: {imported}.");
                               refreshTick.Set(refreshTick.Value + 1);
                           }
                           catch (Exception ex)
                           {
                               statusMessage.Set($"CSV import failed: {ex.Message}");
                           }
                       }))
               | new Card(
                   Layout.Vertical()
                   | Text.H3("Dataset Selection")
                   | (datasetsQuery.Loading
                       ? Skeleton.Card()
                       : simulationOptions.Length == 0
                           ? Text.Muted("No simulation datasets found.")
                           : selectedSimulation.ToSelectInput(simulationOptions))
                   | (datasetsQuery.Loading
                       ? Skeleton.Card()
                       : fieldOptions.Length == 0
                           ? Text.Muted("No field datasets found.")
                           : selectedField.ToSelectInput(fieldOptions))
                   | new Button("Run Comparison")
                       .Primary()
                       .Disabled(!canRunComparison)
                       .OnClick(async () =>
                       {
                           try
                           {
                               if (!canRunComparison)
                               {
                                   statusMessage.Set("Select both datasets before running comparison.");
                                   return;
                               }

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
               | (datasetsQuery.Error is { } queryError
                   ? Callout.Warning($"Failed to load datasets: {queryError.Message}")
                   : new Fragment())
               | (string.IsNullOrWhiteSpace(statusMessage.Value) ? new Fragment() : Callout.Info(statusMessage.Value))
               | (latestResult.Value is null
                   ? new Fragment()
                   : BuildSummaryCard(latestResult.Value))
               | (latestResult.Value is null
                   ? new Fragment()
                   : new Card(
                       Layout.Vertical()
                       | Text.H3("Metrics")
                       | Text.Block($"MAE: {latestResult.Value.Mae:F3} (average absolute mismatch)")
                       | Text.Block($"RMSE: {latestResult.Value.Rmse:F3} (penalizes larger spikes)")
                       | Text.Block($"MRE: {latestResult.Value.MeanRelativeErrorPercent:F2}% (average relative mismatch)")
                       | Text.Block($"P95 |error|: {latestResult.Value.P95AbsoluteError:F3} (95th percentile)")
                       | Text.Block($"Significant points: {latestResult.Value.SignificantDifferenceCount}/{latestResult.Value.TotalComparedPoints}")
                       | Text.Muted(InterpretMetrics(latestResult.Value))))
               | (latestResult.Value is null
                   ? new Fragment()
                   : new Card(
                       Layout.Vertical()
                       | Text.H3("Top Differences")
                       | new List(latestResult.Value.TopDifferences.Select(x =>
                           new ListItem(
                               $"{x.Timestamp:HH:mm} | {x.FrequencyBand} Hz | model={x.SimulationValue:F2}, field={x.FieldValue:F2}, rel={x.RelativeErrorPercent:F1}% | {x.Severity.ToUpperInvariant()}. {x.Explanation}")))))
               | (latestResult.Value is null
                   ? new Fragment()
                   : new Card(
                       Layout.Vertical()
                       | Text.H3("Recommendations")
                       | new List(latestResult.Value.Recommendations.Select(x =>
                           new ListItem($"{FriendlyReasonCode(x.ReasonCode)} ({x.Confidence:P0}). {x.SuggestedAction}")))));
    }

    private static string ToOption(SmartEnergyExpert.Client.Services.DatasetDto dataset) =>
        $"{dataset.Name} | {dataset.SourceSystem} | {dataset.SampleCount} samples [{dataset.Id}]";

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

    private static object BuildSummaryCard(SmartEnergyExpert.Client.Services.ComparisonResultDto result)
    {
        var verdict = result.SignificantDifferenceCount switch
        {
            0 => "Model matches field data well for this run.",
            <= 10 => "Model is mostly aligned, but a few points need review.",
            _ => "Model requires tuning before relying on these predictions."
        };

        return new Card(
            Layout.Vertical()
            | Text.H3("Quick Summary")
            | Text.Block(verdict));
    }

    private static string InterpretMetrics(SmartEnergyExpert.Client.Services.ComparisonResultDto result)
    {
        if (result.MeanRelativeErrorPercent <= 5 && result.SignificantDifferenceCount == 0)
        {
            return "Interpretation: low mismatch level. This run is suitable as baseline evidence.";
        }

        if (result.MeanRelativeErrorPercent <= 15)
        {
            return "Interpretation: moderate mismatch. Review top differences before final conclusion.";
        }

        return "Interpretation: high mismatch. Recalibration or model/environment parameter update is recommended.";
    }

    private static string FriendlyReasonCode(string code) =>
        code switch
        {
            "MODEL_ACCEPTABLE" => "Model is acceptable",
            "FREQ_MODEL_MISMATCH" => "Frequency response mismatch",
            "CALIBRATION_DRIFT" => "Calibration drift suspected",
            "ENVIRONMENT_VARIANCE" => "Environment mismatch suspected",
            _ => code
        };

    private static string BuildSampleCsv()
    {
        var start = DateTimeOffset.UtcNow.AddMinutes(-10);
        var rows = new List<string>
        {
            "timestamp,frequencyBand,amplitudeDb,depthMeters,rangeMeters,soundSpeed,noiseLevelDb"
        };

        for (var i = 0; i < 10; i++)
        {
            var timestamp = start.AddMinutes(i).ToString("o");
            rows.Add($"{timestamp},400,{-71 + i * 0.1m:F3},60,{1000 + i * 10},1498.0,-89.0");
            rows.Add($"{timestamp},800,{-69 + i * 0.1m:F3},60,{1000 + i * 10},1498.1,-88.5");
        }

        return string.Join("\n", rows);
    }
}
