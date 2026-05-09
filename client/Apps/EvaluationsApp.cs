using ClientServices = SmartEnergyExpert.Client.Services;

namespace SmartEnergyExpert.Client.Apps;

[App(icon: Icons.Waves, title: "Hydroacoustic Comparison", searchHints: ["hydroacoustic", "comparison", "charts", "blades", "signal", "explorer", "dataset overview"])]
public sealed class EvaluationsApp : ViewBase
{
    public override object? Build() => UseBlades(() => new WorkspaceBlade(), "Hydroacoustic Comparison");

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
            var simOptions = datasets.Where(x => IsSimulationType(x.Type)).Select(ToOption).ToArray();
            var fieldOptions = datasets.Where(x => IsFieldType(x.Type)).Select(ToOption).ToArray();
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
                       | new Card(
                           Layout.Vertical().Gap(1)
                           | (Layout.Horizontal().Gap(2)
                               | Text.H3("Selection")
                               | new Button("Refresh dataset list")
                                   .Disabled(datasetsQuery.Loading)
                                   .OnClick(() => refreshTick.Set(refreshTick.Value + 1)))
                           | Text.Muted("After importing CSV or generating a simulation elsewhere, tap refresh so new datasets appear.")
                           | (datasetsQuery.Loading
                               ? Skeleton.Card()
                               : simOptions.Length == 0
                                   ? Callout.Warning(
                                       "No simulation datasets loaded. Create one via Environment simulation (type simulation), then refresh.")
                                   : selectedSimulation.ToSelectInput(simOptions))
                           | Text.Muted("Simulation — only type simulation (model branch). Not for ARLUT CSV; use Field below.")
                           | (datasetsQuery.Loading
                               ? Skeleton.Card()
                               : fieldOptions.Length == 0
                                   ? Callout.Warning(
                                       "No field datasets loaded. Import a field CSV in Datasets management or use the seeded ARLUT dataset, then refresh.")
                                   : selectedField.ToSelectInput(fieldOptions))
                           | Text.Muted(
                               "Field — measurements. data/ARLUT_01_partA_01_dataset_field_stride2500.csv is loaded at API startup as dataset name: ARLUT 01 part A field stride2500 (if missing)."))
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
                       | (result.Value is null ? new Fragment() : new ComparisonResultsSection(result.Value));
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
                    | BuildExplorerHalfPanel("Field (measurement)", fieldExplorerQuery))
                | BuildCrossDatasetResolutionHint(simulationExplorerQuery, fieldExplorerQuery));
        }

        private static object BuildCrossDatasetResolutionHint(
            QueryResult<ClientServices.DatasetSignalOverviewDto?> simulationExplorerQuery,
            QueryResult<ClientServices.DatasetSignalOverviewDto?> fieldExplorerQuery)
        {
            if (simulationExplorerQuery.Loading
                || fieldExplorerQuery.Loading
                || simulationExplorerQuery.Error is not null
                || fieldExplorerQuery.Error is not null)
            {
                return new Fragment();
            }

            var sim = simulationExplorerQuery.Value;
            var field = fieldExplorerQuery.Value;
            if (sim is null || field is null || sim.SampleCount == 0 || field.SampleCount == 0)
            {
                return new Fragment();
            }

            var nSim = sim.SampleCount;
            var nField = field.SampleCount;
            var minN = Math.Max(Math.Min(nSim, nField), 1);
            var countSkew = Math.Max(nSim, nField) / minN;
            var ratioFieldPerSim = (decimal)nField / Math.Max(nSim, 1);

            if (countSkew >= 10m)
            {
                return Callout.Warning(
                    $"Very different sample counts ({nSim} simulation vs {nField} field, about {countSkew:F0}×). "
                    + "That almost always means two different CSVs or experiments — the engine falls back on experiment-progress pairing (normalized time), "
                    + "not identical rows. "
                    + "For aligned rows and sane metrics: in Environment simulation choose Mirror → the SAME field dataset you compare here, "
                    + "generate a new simulation, then select both tied to that recording.");
            }

            var pairingNote = ratioFieldPerSim >= 8m
                ? "Field is much denser (≥8×): one field observation per simulation row where alignable; if field density is extreme, ordered field subsampling by time quantiles is used."
                : "Fewer counts on one branch: pairing picks one counterpart per simulation sample (see comparison docs). ";

            return Callout.Info(
                $"Resolution check: simulation {nSim} vs field {nField} samples (field/sim ≈ {ratioFieldPerSim:F2}). "
                + pairingNote);
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
                       | Text.Muted("Use Datasets management to upload a CSV (FileInput) and refresh this view.");
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

        private sealed class ComparisonResultsSection(ClientServices.ComparisonResultDto result) : ViewBase
        {
            public override object? Build()
            {
                var (mismatchSheetView, openMismatchSheet) = UseTrigger((IState<bool> isOpen) =>
                    isOpen.Value
                        ? new Sheet(
                            _ => isOpen.Set(false),
                            Layout.Vertical().Gap(3)
                            | BuildTemporalClustersBlock(result)
                            | BuildTopDifferencesBlock(result),
                            title: "Temporal clusters & top differences",
                            description: "Burst clusters and ranked outlier samples for this comparison run.")
                            .Width(Size.Fraction(2f / 3f))
                        : null);

                var (metricsSheetView, openMetricsSheet) = UseTrigger((IState<bool> isOpen) =>
                    isOpen.Value
                        ? new Sheet(
                            _ => isOpen.Set(false),
                            BuildMetricsSheetBody(result),
                            title: "Metrics",
                            description: "Residual statistics for paired simulation vs field samples and how to interpret them.")
                            .Width(Size.Fraction(2f / 3f))
                        : null);

                var recommendationsStack = Layout.Vertical().Gap(2);
                foreach (var rec in result.Recommendations)
                {
                    recommendationsStack |= BuildRecommendationCard(rec);
                }

                var timelineNote = result.TotalComparedPoints > 0 && result.TimelineNormalizationApplied
                    ? Text.Muted(
                        "Pairs used automatic experiment-progress alignment: each CSV’s timestamps were mapped to u∈[0,1] by its own first/last sample, then aligned by closest u across alignable bands (no shared UTC required). Prefer identical UTC/elapsed-second baselines when you need strict traceability.")
                    : (object)new Fragment();

                return Layout.Vertical().Gap(2)
                       | new Card(
                           Layout.Vertical().Gap(2)
                           | Text.H3("Quick Summary")
                           | Text.Block(result.TotalComparedPoints == 0
                               ? "No paired samples after band scaling (10^n Hz), UTC nearest-match, and experiment-progress pairing (matching normalized elapsed share u per dataset). Check alignable bands and non-empty spans; see findings below."
                               : result.SignificantDifferenceCount == 0
                                   ? "Model matches field data well for this run (on paired points only)."
                                   : "Model needs tuning for part of the compared points.")
                           | timelineNote
                           | Text.H4("Decision support (recommendations)")
                           | Text.Muted(
                               "Each finding is a rule-engine hypothesis with explicit evidence — not an ML black box.")
                           | Text.Muted(
                               "Confidence is a deterministic interpretability score (0–1) from threshold rules on this run — not an ML probability. Higher values mean more independent signals agreed on the same hypothesis.")
                           | Text.H4("Висновки")
                           | (result.Recommendations.Length == 0
                               ? Text.Muted("No recommendations returned for this run.")
                               : recommendationsStack))
                       | (Layout.Horizontal().Gap(2)
                           | new Button("View temporal clusters & top differences")
                               .OnClick(_ => openMismatchSheet())
                           | new Button("View metrics")
                               .OnClick(_ => openMetricsSheet()))
                       | mismatchSheetView
                       | metricsSheetView;
            }
        }

        private static object BuildMetricsSheetBody(ClientServices.ComparisonResultDto result)
        {
            var hasPairs = result.TotalComparedPoints > 0;

            object ValueLine(string line) =>
                hasPairs ? Text.Block(line) : Text.Muted("— no paired samples yet for this run.");

            return Layout.Vertical().Gap(3)
                   | (hasPairs
                       ? Text.Muted(
                           "Values are computed only on paired points that survived alignment and overlap checks.")
                       : Callout.Warning(
                           "Compared points: 0 — pairing tries exact row, then band-scaled UTC nearest (skew cap), then experiment-progress pairing (min |u_sim−u_field|). Values appear below once pairs exist."))
                   | Text.Block("MAE · Mean Absolute Error").Bold()
                   | Text.Muted(
                       "Mean of absolute dB gaps between simulator and field on each paired sample. Describes typical error magnitude "
                       + "without direction; robust to outliers compared with RMSE, but treats every sample equally.")
                   | ValueLine($"MAE: {result.Mae:F3} dB")
                   | Text.Block("RMSE · Root Mean Square Error").Bold()
                   | Text.Muted(
                       "Square root of the mean squared dB residuals. Highlights larger disagreements—big spikes inflate RMSE faster than MAE—"
                       + "useful when a few catastrophic mismatches matter.")
                   | ValueLine($"RMSE: {result.Rmse:F3} dB")
                   | Text.Block("MRE · Mean Relative Error").Bold()
                   | Text.Muted(
                       "Average absolute relative discrepancy versus the observed field SPL, expressed as a percent for this prototype. "
                       + "Interpret together with amplitude scale: very low SNR bins can mechanically inflate percentages.")
                   | ValueLine($"MRE: {result.MeanRelativeErrorPercent:F2}%")
                   | Text.Block("P95 · 95th percentile absolute error").Bold()
                   | Text.Muted(
                       "The residual dB value such that roughly 95% of paired mismatches lie below it—a tail-focused summary complementary to MAE/RMSE.")
                   | ValueLine($"P95 absolute error: {result.P95AbsoluteError:F3} dB")
                   | Text.Block("Significant points").Bold()
                   | Text.Muted(
                       "Count of paired samples classified as materially different versus total paired observations. "
                       + "Buckets use relative amplitude thresholds; see severity legend.")
                   | ValueLine(
                       $"Significant points: {result.SignificantDifferenceCount} / {result.TotalComparedPoints} paired samples "
                       + "(relative amplitude error severity tiers)")
                   | Text.H4("Severity buckets (relative amplitude error)")
                   | Text.Muted(
                       "LOW < 2%; MODERATE 2–5%; HIGH 5–10%; CRITICAL > 10%. "
                       + "Severity labels drive the numerator of “significant” counts and help triage hotspots.");
        }

        private static object BuildTemporalClustersBlock(ClientServices.ComparisonResultDto result) =>
            Layout.Vertical().Gap(1)
            | Text.H4("Temporal clusters (Top mismatches)")
            | (result.TemporalClusters.Length == 0
                ? Text.Muted("Clusters appear when bursts of samples share the same band and timestamps within ~75ms.")
                : new List(result.TemporalClusters.Select(c =>
                    new ListItem(
                        $"Cluster #{c.Ordinal}: {c.TimeStart:HH:mm:ss.fff}–{c.TimeEnd:HH:mm:ss.fff} | {c.FrequencyBand} Hz | "
                        + $"n={c.PointCount}, mean rel err {c.MeanRelativeErrorPercent:F1}%"))));

        private static object BuildTopDifferencesBlock(ClientServices.ComparisonResultDto result) =>
            Layout.Vertical().Gap(1)
            | Text.H4("Top Differences")
            | (result.TopDifferences.Length == 0
                ? Text.Muted("No outlier rows in the Top-N list.")
                : new List(result.TopDifferences.Select(x =>
                    new ListItem(
                        $"{x.Timestamp:HH:mm:ss.fff} | {x.FrequencyBand} Hz | rel={x.RelativeErrorPercent:F1}% | {x.Severity.ToUpperInvariant()}"))));

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
            "DATA_ALIGNMENT" => "Data alignment / pairing",
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
                   | (result.TotalComparedPoints == 0
                       ? Callout.Warning(
                           "No paired samples — charts below are empty or all-zero and do not indicate model quality.")
                       : new Fragment())
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
                       | Text.Muted("Bars mimic a heat-intensity ranking: tallest cells are the hottest frequency-time buckets."));
        }
    }

    private static bool IsSimulationType(string type) =>
        string.Equals(type, "simulation", StringComparison.OrdinalIgnoreCase);

    private static bool IsFieldType(string type) =>
        string.Equals(type, "field", StringComparison.OrdinalIgnoreCase);

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
