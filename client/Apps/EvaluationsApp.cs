using ClientServices = SmartEnergyExpert.Client.Services;

namespace SmartEnergyExpert.Client.Apps;

[App(icon: Icons.Waves, title: "Hydroacoustic Comparison", searchHints: ["hydroacoustic", "comparison", "charts", "blades", "preset", "signal", "explorer", "dataset overview"])]
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
            var simulationExplorerQuery = UseQuery(
                key: ("sim-signal-explorer", refreshTick.Value, selectedSimulation.Value),
                fetcher: async ct =>
                {
                    var id = TryParseDatasetId(selectedSimulation.Value);
                    if (id == Guid.Empty)
                    {
                        return (ClientServices.DatasetSignalOverviewDto?)null;
                    }

                    return await apiClient.GetDatasetSignalOverviewAsync(id, ct);
                });
            var fieldExplorerQuery = UseQuery(
                key: ("field-signal-explorer", refreshTick.Value, selectedField.Value),
                fetcher: async ct =>
                {
                    var id = TryParseDatasetId(selectedField.Value);
                    if (id == Guid.Empty)
                    {
                        return (ClientServices.DatasetSignalOverviewDto?)null;
                    }

                    return await apiClient.GetDatasetSignalOverviewAsync(id, ct);
                });
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
                           | Text.Muted("Simulation dataset: the modeled hydroacoustic signal output.")
                           | (datasetsQuery.Loading ? Skeleton.Card() : selectedField.ToSelectInput(fieldOptions))
                           | Text.Muted("Field dataset: the measured signal from the real experiment."))
                       | BuildSignalExplorerCard(simulationExplorerQuery, fieldExplorerQuery)
                       | new Card(
                           Layout.Vertical()
                           | Text.H3("Compare")
                           | topN.ToNumberInput(min: 5, max: 100).Placeholder("Top-N")
                           | Text.Muted("After inspecting both signals, tune Top-N for how many outliers to analyze.")
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

        private static object BuildSignalExplorerCard(
            QueryResult<ClientServices.DatasetSignalOverviewDto?> simulationExplorerQuery,
            QueryResult<ClientServices.DatasetSignalOverviewDto?> fieldExplorerQuery)
        {
            return new Card(
                Layout.Vertical().Gap(2)
                | Text.H3("Signal explorer")
                | Text.Muted("Flow: Dataset → inspect summaries here → interpret structure → Run comparison.")
                | (Layout.Horizontal().Gap(4)
                    | BuildExplorerHalfPanel("Simulation (model)", simulationExplorerQuery)
                    | BuildExplorerHalfPanel("Field (measurement)", fieldExplorerQuery)));
        }

        private static object BuildExplorerHalfPanel(string role, QueryResult<ClientServices.DatasetSignalOverviewDto?> query)
        {
            if (query.Loading)
            {
                return Layout.Vertical().Gap(1).Width(Size.Fraction(0.48f))
                       | Text.H4(role)
                       | Skeleton.Card();
            }

            if (query.Error is { } err)
            {
                return Layout.Vertical().Gap(1).Width(Size.Fraction(0.48f))
                       | Text.H4(role)
                       | Callout.Warning(err.Message);
            }

            var o = query.Value;
            if (o is null)
            {
                return Layout.Vertical().Gap(1).Width(Size.Fraction(0.48f))
                       | Text.H4(role)
                       | Text.Muted("Select a dataset in the list above to load signal-level statistics.");
            }

            if (o.SampleCount == 0)
            {
                return Layout.Vertical().Gap(1).Width(Size.Fraction(0.48f))
                       | Text.H4(role)
                       | Text.Block($"{o.Name} ({o.SourceSystem}) — no acoustic samples imported yet.")
                       | Text.Muted("Use Datasets management to import CSV samples and refresh this view.");
            }

            var durationText = o.DurationSeconds <= 0.0001m && o.SampleCount > 1
                ? $"{o.SampleCount} samples aligned to overlapping timestamps."
                : $"{o.SampleCount} samples spanning {FormatDurationHuman(o.DurationSeconds)}.";

            return Layout.Vertical().Gap(1).Width(Size.Fraction(0.48f))
                   | Text.H4(role)
                   | Text.Block(o.Name).Bold()
                   | Text.Block(durationText)
                   | Text.Block(
                       $"Frequency range: {FormatFrequencyRangeSummary(o.FrequencyMinHz, o.FrequencyMaxHz)} "
                       + $"({o.DistinctFrequencyBins} distinct bins)")
                   | Text.Block($"Peak amplitude: {o.PeakAmplitudeDb:F2} dB")
                   | Text.Block(
                       $"Noise floor (≈10th percentile amplitude): {o.NoiseFloorDb:F2} dB · Mean level: {o.MeanAmplitudeDb:F2} dB")
                   | (o.MeanNoiseLevelDb is null
                       ? Text.Muted("Measured noise-level column absent or empty.")
                       : Text.Block($"Recorded noise telemetry (avg): {o.MeanNoiseLevelDb.Value:F2} dB"));
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
                | Text.Muted("Enter a human-readable name for this reusable comparison configuration.")
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
            var recommendationsStack = Layout.Vertical().Gap(2);
            foreach (var rec in result.Recommendations)
            {
                recommendationsStack |= BuildRecommendationCard(rec);
            }

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
                       | Text.Block($"Significant points: {result.SignificantDifferenceCount}/{result.TotalComparedPoints}")
                       | Text.Muted("Severity buckets use relative amplitude error (%): LOW < 2%; MODERATE 2–5%; HIGH 5–10%; CRITICAL > 10%."))
                   | new Card(
                       Layout.Vertical()
                       | Text.H3("Temporal clusters (Top mismatches)")
                       | (result.TemporalClusters.Length == 0
                           ? Text.Muted("Clusters appear when bursts of samples share the same band and timestamps within ~75ms.")
                           : new List(result.TemporalClusters.Select(c =>
                               new ListItem(
                                   $"Cluster #{c.Ordinal}: {c.TimeStart:HH:mm:ss.fff}–{c.TimeEnd:HH:mm:ss.fff} | {c.FrequencyBand}Hz | "
                                   + $"n={c.PointCount}, mean rel err {c.MeanRelativeErrorPercent:F1}%")))))
                   | new Card(
                       Layout.Vertical()
                       | Text.H3("Top Differences")
                       | new List(result.TopDifferences.Take(12).Select(x =>
                           new ListItem($"{x.Timestamp:HH:mm:ss.fff} | {x.FrequencyBand}Hz | rel={x.RelativeErrorPercent:F1}% | {x.Severity.ToUpperInvariant()}"))))
                   | new Card(
                       Layout.Vertical()
                       | Text.H3("Decision support (recommendations)")
                       | Text.Muted("Each finding is a rule-engine hypothesis with explicit evidence — not an ML black box.")
                       | recommendationsStack);
        }

        private static object BuildRecommendationCard(ClientServices.RecommendationDto rec)
        {
            var title = $"{CategoryTitle(rec.Category)} · {rec.ReasonCode.Replace('_', ' ')}";
            return new Card(
                Layout.Vertical().Gap(1)
                       | Text.H4(title)
                       | Text.Block($"Confidence: {rec.Confidence:P0}")
                       | Text.Muted($"Method: {rec.InferenceMethod}")
                       | Text.Block(rec.ConfidenceRationale)
                       | Text.Block(rec.Explanation)
                       | Text.H4("Evidence")
                       | (rec.EvidenceSignals.Length == 0
                           ? Text.Muted("No structured evidence rows.")
                           : new List(rec.EvidenceSignals.Select(s => new ListItem(s))))
                       | Text.Block($"Suggested action: {rec.SuggestedAction}"));
        }

        private static string CategoryTitle(string category) => category switch
        {
            "ENVIRONMENT_VARIANCE" => "Environment / SSP & noise coupling",
            "SENSOR_DRIFT" => "Sensor calibration bias",
            "MODEL_MISMATCH" => "Global propagation model mismatch",
            "NOISE_INTERFERENCE" => "Broadband noise / episodic masking",
            "FREQUENCY_ATTENUATION" => "Band-limited coupling / attenuation tilt",
            "ACCEPTABLE_MODEL" => "Acceptable agreement",
            _ => category
        };
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
                .Select(x => new
                {
                    Time = x.Timestamp.ToString("HH:mm:ss.fff"),
                    RelativeError = (double)x.RelativeErrorPercent,
                    AbsoluteError = (double)x.AbsoluteError
                })
                .ToArray();

            var severityRows = result.TopDifferences
                .GroupBy(x => x.Severity)
                .Select(g => new { Severity = g.Key, Count = g.Count() })
                .ToArray();

            var overlayBand = result.OverlaySeries.FirstOrDefault()?.FrequencyBand ?? 0m;
            var overlayRows = result.OverlaySeries
                .Select(x => new
                {
                    Time = x.Timestamp.ToString("HH:mm"),
                    Sim = (double)x.SimulationDb,
                    Field = (double)x.FieldDb
                })
                .ToArray();

            var heatmapRows = result.MismatchHeatmap
                .OrderByDescending(x => x.MaxRelativeErrorPercent)
                .Take(32)
                .Select(x => new
                {
                    Cell = $"{x.FrequencyBand}Hz @ {x.TimeBucket}",
                    Error = (double)x.MaxRelativeErrorPercent
                })
                .ToArray();

            return Layout.Vertical().Gap(2)
                   | Text.H3("Charts")
                   | new Card(
                       Layout.Vertical()
                       | Text.Block("Metric comparison")
                       | metricRows.ToBarChart(
                           e => e.Metric,
                           [e => e.Sum(v => v.Value)],
                           BarChartStyles.Default)
                       | Text.Muted("This chart summarizes global quality indicators for the current run."))
                   | new Card(
                       Layout.Vertical()
                       | Text.Block(
                           $"Overlay: simulation vs field (dominant scrutiny band ~ {overlayBand} Hz)")
                       | (overlayRows.Length == 0
                           ? Text.Muted("No overlay samples returned for this run.")
                           : overlayRows.ToLineChart(
                               e => e.Time,
                               [e => e.Sum(v => v.Sim), e => e.Sum(v => v.Field)],
                               LineChartStyles.Dashboard))
                       | Text.Muted("Two amplitude traces on the dominant mismatch band illustrate where the simulator tracks the recorder."))
                   | new Card(
                       Layout.Vertical()
                       | Text.Block("Mismatch heatmap (minute × frequency, max relative error)")
                       | (heatmapRows.Length == 0
                           ? Text.Muted("No heatmap aggregates available.")
                           : heatmapRows.ToBarChart(
                               e => e.Cell,
                               [e => e.Sum(v => v.Error)],
                               BarChartStyles.Default))
                       | Text.Muted("Bars mimic a heat-intensity ranking: tallest cells are the hottest frequency-time buckets."))
                   | new Card(
                       Layout.Vertical()
                       | Text.Block("Top difference trend: relative vs absolute")
                       | trendRows.ToLineChart(
                           e => e.Time,
                           [e => e.Sum(v => v.RelativeError), e => e.Sum(v => v.AbsoluteError)],
                           LineChartStyles.Dashboard)
                       | Text.Muted("Relative and absolute errors for the fiercest outliers."))
                   | new Card(
                       Layout.Vertical()
                       | Text.Block("Severity distribution (Top-N)")
                       | severityRows.ToPieChart(
                           e => e.Severity,
                           e => e.Sum(v => v.Count),
                           PieChartStyles.Default)
                       | Text.Muted("Share of LOW/MODERATE/HIGH/CRITICAL among the surfaced Top-N list."));
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

    private static string FormatFrequencyHz(decimal hz) =>
        hz >= 1000m ? $"{hz / 1000m:N1} kHz" : $"{hz:N0} Hz";

    private static string FormatFrequencyRangeSummary(decimal minHz, decimal maxHz) =>
        $"{FormatFrequencyHz(minHz)} – {FormatFrequencyHz(maxHz)}";

    private static string FormatDurationHuman(decimal seconds)
    {
        if (seconds >= 7200)
        {
            return $"{seconds / 3600m:N1} h";
        }

        if (seconds >= 120)
        {
            return $"{seconds / 60m:N1} min";
        }

        return $"{seconds:N3} s";
    }
}
